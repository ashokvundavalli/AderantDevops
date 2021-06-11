using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver.Model;
using Microsoft.FSharp.Collections;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class PaketLockOperations {

        internal ResolverRequest ResolverRequest;
        private readonly string lockFilePath;

        internal LockFile LockFileContent;
        private FSharpMap<Tuple<Domain.GroupName, Domain.PackageName>, PackageResolver.PackageInfo> groupedResolution;

        internal FSharpMap<Tuple<Domain.GroupName, Domain.PackageName>, PackageResolver.PackageInfo> GroupedResolution {
            get {
                if (groupedResolution != null) {
                    return groupedResolution;
                }

                if (LockFileContent != null) {
                    groupedResolution = LockFileContent.GetGroupedResolution();
                }

                return groupedResolution;
            }
        }

        internal PaketLockOperations(ResolverRequest resolverRequest, string lockFilePath, IFileSystem fileSystem) {
            ErrorUtilities.IsNotNull(fileSystem, nameof(fileSystem));

            if (string.IsNullOrWhiteSpace(lockFilePath)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(lockFilePath));
            }

            this.ResolverRequest = resolverRequest ?? throw new ArgumentNullException(nameof(resolverRequest));
            this.lockFilePath = Path.GetFullPath(lockFilePath);

            LockFileContent = LoadLockFile(lockFilePath, fileSystem);
        }

        internal PaketLockOperations(string lockFileName, string[] lockFileContent) {
            ErrorUtilities.IsNotNull(lockFileContent, nameof(lockFileContent));

            if (string.IsNullOrWhiteSpace(lockFileName)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(lockFileName));
            }

            LockFileContent = LockFile.Parse(lockFileName, lockFileContent);
        }

        internal List<PackageGroup> GetPackageInfo() {
            if (GroupedResolution == null) {
                return new List<PackageGroup>(0);
            }

            var groups = GroupedResolution.GroupBy(x => x.Key.Item1.Name);

            List<PackageGroup> packageInfoGroups = new List<PackageGroup>();

            foreach (var group in groups) {
                packageInfoGroups.Add(new PackageGroup(group.Key, group.Select(x => new PackageInfo(x.Key.Item2.Name, x.Value.Version.AsString)).ToList()));
            }

            return packageInfoGroups;
        }

        internal static LockFile LoadLockFile(string lockFile, IFileSystem fileSystem) {
            if (!fileSystem.FileExists(lockFile)) {
                // paket.lock file does not exist.
                return null;
            }

            return LockFile.LoadFrom(lockFile);
        }

        internal void SaveLockFileForModules() {
            if (LockFileContent == null) {
                return;
            }

            foreach (ExpertModule module in ResolverRequest.Modules) {
                SaveModuleSpecificLockFile(module);
            }
        }

        private void SaveModuleSpecificLockFile(ExpertModule module) {
            if (!module.DependencyRequirements.Any()) {
                return;
            }

            string tempLockFilePath = Path.Combine(Path.GetFullPath(module.FullPath), Constants.PaketLock);

            if (string.Equals(lockFilePath, tempLockFilePath)) {
                // Don't save over an existing lock file.
                return;
            }

            List<string> groups = module.DependencyRequirements.Select(x => x.Group).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var resolvedPackages = GetResolvedPackages(module.DependencyRequirements);

            FSharpMap<Domain.GroupName, LockFileGroup> generatedGroups = AssociateRequirementsWithGroups(groups, resolvedPackages);

            if (generatedGroups.IsEmpty) {
                // Nothing to save.
                return;
            }

            LockFile tempLockFile = new LockFile(tempLockFilePath, generatedGroups);
            tempLockFile.Save();
        }

        private List<Tuple<string, PackageResolver.PackageInfo>> GetResolvedPackages(IList<IDependencyRequirement> dependencyRequirements) {
            List<Tuple<string, PackageResolver.PackageInfo>> resolvedPackages = new List<Tuple<string, PackageResolver.PackageInfo>>();

            if (GroupedResolution == null) {
                return resolvedPackages;
            }

            // Remote files are incompatible.
            foreach (var dependencyRequirement in dependencyRequirements.Where(x => !(x is RemoteFile))) {
                // Map dependency requirement to resolved PackageInfo.
                var pair = Tuple.Create(Domain.GroupName(dependencyRequirement.Group), Domain.PackageName(dependencyRequirement.Name));

                if (GroupedResolution.ContainsKey(pair)) {
                    var packageInfo = GroupedResolution[pair];
                    resolvedPackages.Add(Tuple.Create(dependencyRequirement.Group, packageInfo));
                }
            }

            return resolvedPackages;
        }

        private FSharpMap<Domain.GroupName, LockFileGroup> AssociateRequirementsWithGroups(IList<string> groups, List<Tuple<string, PackageResolver.PackageInfo>> resolvedPackages) {
            FSharpMap<Domain.GroupName, LockFileGroup> generatedGroups = new FSharpMap<Domain.GroupName, LockFileGroup>(new List<Tuple<Domain.GroupName, LockFileGroup>>());

            foreach (string group in groups) {
                Domain.GroupName groupName = Domain.GroupName(group);
                // Filter packages in groups.
                var groupedPackages = resolvedPackages.Where(x => string.Equals(group, x.Item1, StringComparison.OrdinalIgnoreCase));

                var resolution = new FSharpMap<Domain.PackageName, PackageResolver.ResolvedPackage>(new List<Tuple<Domain.PackageName, PackageResolver.ResolvedPackage>>());

                foreach (var package in groupedPackages) {
                    resolution = new FSharpMap<Domain.PackageName, PackageResolver.ResolvedPackage>(resolution.Select(x => Tuple.Create(x.Key, x.Value)).Append(Tuple.Create(package.Item2.Name,
                        new PackageResolver.ResolvedPackage(package.Item2.Name, package.Item2.Version,
                            package.Item2.Dependencies, package.Item2.Unlisted, package.Item2.IsRuntimeDependency,
                            package.Item2.Kind, package.Item2.Settings, package.Item2.Source))));
                }

                LockFileGroup generatedGroup = new LockFileGroup(groupName, LockFileContent.GetGroup(groupName).Options, resolution, FSharpList<ModuleResolver.ResolvedSourceFile>.Empty);

                generatedGroups = new FSharpMap<Domain.GroupName, LockFileGroup>(generatedGroups.Select(x => Tuple.Create(x.Key, x.Value)).Append(Tuple.Create(groupName, generatedGroup)));
            }

            return generatedGroups;
        }

        internal static string HashLockFile(LockFile lockFile, string[] packageHashVersionExclusions) {
            var groupedResolutionToHash = lockFile.GetGroupedResolution();

            using (var stream = new MemoryStream()) {
                using (var writer = new StreamWriter(stream)) {
                    foreach (var item in groupedResolutionToHash) {
                        // if names match, blank the version
                        string version;
                        if (packageHashVersionExclusions != null && packageHashVersionExclusions.Any(x => string.Equals(item.Key.Item2.Name, x, StringComparison.OrdinalIgnoreCase))) {
                            version = string.Empty;
                        } else {
                            version = item.Value.Version.AsString;
                        }

                        writer.Write(item.Key.Item1.Name.ToUpperInvariant());
                        writer.Write(item.Key.Item2.Name.ToUpperInvariant());
                        writer.Write(version.ToUpperInvariant());
                    }

                    writer.Flush();

                    stream.Position = 0;
                    return stream.ComputeSha1Hash();
                }
            }
        }
    }
}
