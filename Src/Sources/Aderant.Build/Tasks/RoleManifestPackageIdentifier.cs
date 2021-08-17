using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class RoleManifestPackageIdentifier : Task {


        private static readonly ConcurrentDictionary<string, List<RoleManifest>> fileContentPerDirectory = new ConcurrentDictionary<string, List<RoleManifest>>();

        private static readonly ConcurrentDictionary<string, RoleManifest> roleManifests = new ConcurrentDictionary<string, RoleManifest>(StringComparer.OrdinalIgnoreCase);


        [Required]
        public string[] DependentRoles { get; set; }

        [Required]
        public string[] ManifestSearchDirectories { get; set; }

        [Output]
        public ITaskItem[] DependentPackages { get; set; }

        public string[] PackageBlacklist { get; set; } = Array.Empty<string>();


        public override bool Execute() {
            if (DependentRoles == null || DependentRoles.Length == 0) {
                return !Log.HasLoggedErrors;
            }

            var locatedRoles = LocateRoleManifests(ManifestSearchDirectories);
            foreach (RoleManifest role in locatedRoles) {
                roleManifests.TryAdd(role.Name, role);
            }

            if (roleManifests.Count == 0) {
                Log.LogError("Unable to locate dependent role manifest files.");
                return !Log.HasLoggedErrors;
            }

            DuplicateRoleFilesPresent(roleManifests.Values.ToArray(), Log);

            var dependentPackages = new List<string>();
            var processedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string role in DependentRoles) {
                var roleManifest = roleManifests.Select(x => x.Value).FirstOrDefault(x => x.FilePath.EndsWith($"{role}.role.xml", StringComparison.OrdinalIgnoreCase));

                if (roleManifest == null) {
                    Log.LogError("Unable to locate role: '{0}'. As a result, the integration test assemblies will not contain valid role dependencies.", role);
                    continue;
                }

                foreach (string dependentPackage in roleManifest.GetPackageDependencyTree(roleManifests.Values)) {
                    if (processedPackages.Add(dependentPackage)) {
                        dependentPackages.Add(dependentPackage);
                    }
                }
            }

            // Remove blacklisted packages.
            foreach (string package in PackageBlacklist) {
                dependentPackages.Remove(package);
            }

            List<ITaskItem> taskItems = new List<ITaskItem>(dependentPackages.Count);

            foreach (string package in dependentPackages) {
                ITaskItem taskItem = new TaskItem("System.Reflection.AssemblyMetadataAttribute");
                taskItem.SetMetadata("_Parameter1", "PackageRequirement");
                taskItem.SetMetadata("_Parameter2", package);
                taskItems.Add(taskItem);
            }

            DependentPackages = taskItems.ToArray();

            return !Log.HasLoggedErrors;
        }



        internal static List<RoleManifest> LocateRoleManifests(string[] directories) {
            if (directories == null) {
                throw new ArgumentException("Value cannot be null.", nameof(directories));
            }
            if (directories.Length == 0) {
                throw new ArgumentException("Array must contain elements.", nameof(directories));
            }

            List<RoleManifest> aggregate = new List<RoleManifest>();

            foreach (string directory in directories) {
                string searchDirectory = Path.GetFullPath(directory);

                if (!fileContentPerDirectory.TryGetValue(searchDirectory, out var roleManifests)) {
                    roleManifests = new List<RoleManifest>();

                    if (!Directory.Exists(searchDirectory)) {
                        throw new DirectoryNotFoundException($"Directory not found: '{searchDirectory}'.");
                    }

                    string[] files = Directory.GetFiles(searchDirectory, "*.role.xml", SearchOption.TopDirectoryOnly);

                    foreach (string file in files) {
                        XDocument roleDocument = XDocument.Load(file, LoadOptions.SetLineInfo | LoadOptions.SetBaseUri);
                        roleManifests.Add(new RoleManifest(file, roleDocument));
                    }

                    fileContentPerDirectory.TryAdd(searchDirectory, roleManifests);
                }

                aggregate.AddRange(roleManifests);
            }

            return aggregate;
        }

        internal static bool DuplicateRoleFilesPresent(RoleManifest[] roleManifests, TaskLoggingHelper logger) {
            IEnumerable<IGrouping<string, RoleManifest>> groups = roleManifests.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
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
        private static readonly Regex nameRegex = new Regex(@"\.role$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex regex = new Regex(@"\.zip$", RegexOptions.IgnoreCase);

        private readonly XDocument roleDocument;

        public string FilePath { get; }

        internal string Name { get; }

        internal RoleManifest(string filePath, XDocument roleDocument) {
            ErrorUtilities.IsNotNull(filePath, nameof(filePath));
            ErrorUtilities.IsNotNull(roleDocument, nameof(roleDocument));

            FilePath = filePath;
            Name = nameRegex.Replace(Path.GetFileNameWithoutExtension(filePath), string.Empty);
            this.roleDocument = roleDocument;
        }

        /// <summary>
        /// This method will create a package dependency tree.
        /// The output will be a string array of package names.
        /// </summary>
        public string[] GetPackageDependencyTree(ICollection<RoleManifest> roleManifests) {
            List<string> orderedPackages = new List<string>();

            // Examine the packages node in the role file(s) and extract the package file name.
            HashSet<string> packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Identify the dependent role manifests.
            HashSet<string> dependentRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            dependentRoles.UnionWith(GetValues(roleDocument, "dependencies", "type"));

            List<RoleManifest> roles = new List<RoleManifest>(dependentRoles.Count);
            roles.AddRange(roleManifests.Where(x => dependentRoles.Any(y => string.Equals(y, x.Name, StringComparison.OrdinalIgnoreCase))));

            foreach (RoleManifest role in roles) {
                foreach (string package in role.GetPackageDependencyTree(roleManifests)) {
                    if (packages.Add(package)) {
                        orderedPackages.Add(package);
                    }
                }

                packages.UnionWith(role.GetPackageDependencyTree(roleManifests));
            }

            foreach (string package in GetValues(roleDocument, "packages", "filename")) {
                if (packages.Add(package)) {
                    orderedPackages.Add(package);
                }
            }

            return orderedPackages.ToArray();
        }

        private static string[] GetValues(XDocument roleDocument, string nodeName, string attributeName) {
            HashSet<string> packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            XElement node = roleDocument.Descendants(nodeName).SingleOrDefault();

            if (node != null) {
                foreach (var dependencyDescendant in node.Descendants()) {
                    XAttribute fileName = dependencyDescendant.Attribute(attributeName);
                    if (fileName != null) {

                        packages.Add(regex.Replace(fileName.Value, string.Empty));
                    }
                }
            }

            return packages.ToArray();
        }
    }
}
