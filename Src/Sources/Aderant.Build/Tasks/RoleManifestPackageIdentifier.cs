using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class RoleManifestPackageIdentifier : Task {

        [Required]
        public string[] DependentRoles { get; set; }

        [Required]
        public string[] ManifestSearchDirectories { get; set; }

        [Output]
        public ITaskItem2[] DependentPackages { get; set; }

        public string[] PackageBlacklist { get; set; } = new string[] { };

        internal static ConcurrentDictionary<string, RoleManifest> RoleManifests { get; set; } = new ConcurrentDictionary<string, RoleManifest>(StringComparer.OrdinalIgnoreCase);

        public override bool Execute() {
            if (DependentRoles == null || DependentRoles.Length == 0) {
                return !Log.HasLoggedErrors;
            }

            RoleManifest[] locatedRoles = LocateRoleManifests(ManifestSearchDirectories);
            foreach (RoleManifest role in locatedRoles) {
                RoleManifests.TryAdd(role.Name, role);
            }

            if (RoleManifests.Count == 0) {
                Log.LogError("Unable to locate dependent role manifest files.");
                return !Log.HasLoggedErrors;
            }

            DuplicateRoleFilesPresent(RoleManifests.Values.ToArray(), Log);

            List<string> dependentPackages = new List<string>();
            HashSet<string> processedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string role in DependentRoles) {
                RoleManifest roleManifest = RoleManifests.Select(x => x.Value).FirstOrDefault(x => x.FilePath.EndsWith($"{role}.role.xml", StringComparison.OrdinalIgnoreCase));

                if (roleManifest == null) {
                    Log.LogError("Unable to locate role: '{0}'. As a result, the integration test assemblies will not contain valid role dependencies.", role);
                    continue;
                }

                foreach (string dependentPackage in roleManifest.PackageDependencies) {
                    if (processedPackages.Add(dependentPackage)) {
                        dependentPackages.Add(dependentPackage);
                    }
                }
            }

            // Remove blacklisted packages.
            foreach (string package in PackageBlacklist) {
                dependentPackages.Remove(package);
            }

            List<ITaskItem2> taskItems = new List<ITaskItem2>(dependentPackages.Count);

            foreach (string package in dependentPackages) {
                ITaskItem2 taskItem = new TaskItem("System.Reflection.AssemblyMetadataAttribute");
                taskItem.SetMetadata("_Parameter1", "PackageRequirement");
                taskItem.SetMetadata("_Parameter2", package);
                taskItems.Add(taskItem);
            }

            DependentPackages = taskItems.ToArray();

            return !Log.HasLoggedErrors;
        }

        internal static RoleManifest[] LocateRoleManifests(string[] directories) {
            if (directories == null) {
                throw new ArgumentException("Value cannot be null.", nameof(directories));
            }
            if (directories.Length == 0) {
                throw new ArgumentException("Array must contain elements.", nameof(directories));
            }

            List<RoleManifest> roleManifests = new List<RoleManifest>();
            Regex regex = new Regex(@"\.role$", RegexOptions.IgnoreCase);

            foreach (string directory in directories) {
                string searchDirectory = Path.GetFullPath(directory);

                if (!Directory.Exists(searchDirectory)) {
                    throw new DirectoryNotFoundException($"Directory not found: '{searchDirectory}'.");
                }

                string[] files = Directory.GetFiles(searchDirectory, "*.role.xml", SearchOption.TopDirectoryOnly);

                foreach (string file in files) {
                    roleManifests.Add(new RoleManifest(regex.Replace(Path.GetFileNameWithoutExtension(file), string.Empty), file));
                }
            }

            return roleManifests.ToArray();
        }

        internal static bool DuplicateRoleFilesPresent(IList<RoleManifest> roleManifests, TaskLoggingHelper logger) {
            IEnumerable<IGrouping<string, RoleManifest>> groups = roleManifests.GroupBy(x => x.Name);
            bool duplicates = false;

            foreach (IGrouping<string, RoleManifest> group in groups) {
                if (group.Count() > 1) {
                    duplicates = true;

                    foreach (RoleManifest roleManifest in group) {
                        logger?.LogWarning("Detected duplicate role manifest: '{0}'.", roleManifest.FilePath);
                    }
                }
            }

            return duplicates;
        }
    }

    internal class RoleManifest {
        internal string Name { get; }
        internal string FilePath { get; }
        private string[] packageDependencies;

        // An array of package file name(s) which were extracted from the role file dependency tree.
        internal string[] PackageDependencies => packageDependencies ?? (packageDependencies = GetPackageDependencyTree());

        internal RoleManifest(string name, string filePath) {
            Name = name;
            FilePath = filePath;
        }

        /// <summary>
        /// This method will create a package dependency tree.
        /// The output will be a string array of package names.
        /// </summary>
        /// <returns></returns>
        private string[] GetPackageDependencyTree() {
            XDocument roleDocument = XDocument.Load(FilePath, LoadOptions.SetLineInfo | LoadOptions.SetBaseUri);

            List<string> orderedPackages = new List<string>();

            // Examine the packages node in the role file(s) and extract the package file name.
            HashSet<string> packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Identify the dependent role manifests.
            HashSet<string> dependentRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            dependentRoles.UnionWith(GetValues(roleDocument, "dependencies", "type"));

            List<RoleManifest> roles = new List<RoleManifest>(dependentRoles.Count);
            roles.AddRange(RoleManifestPackageIdentifier.RoleManifests.Values.Where(x => dependentRoles.Any(y => string.Equals(y, x.Name, StringComparison.OrdinalIgnoreCase))));

            foreach (RoleManifest role in roles) {
                foreach (string package in role.PackageDependencies) {
                    if (packages.Add(package)) {
                        orderedPackages.Add(package);
                    }
                }

                packages.UnionWith(role.PackageDependencies);
            }

            foreach (string package in GetValues(roleDocument, "packages", "filename")) {
                if (packages.Add(package)) {
                    orderedPackages.Add(package);
                }
            }

            return orderedPackages.ToArray();
        }

        private string[] GetValues(XDocument roleDocument, string nodeName, string attributeName) {
            HashSet<string> packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            XElement node = roleDocument.Descendants(nodeName).SingleOrDefault();

            if (node != null) {
                foreach (var dependencyDescendant in node.Descendants()) {
                    XAttribute fileName = dependencyDescendant.Attribute(attributeName);
                    if (fileName != null) {
                        Regex regex = new Regex(@"\.zip$", RegexOptions.IgnoreCase);
                        packages.Add(regex.Replace(fileName.Value, string.Empty));
                    }
                }
            }

            return packages.ToArray();
        }
    }
}
