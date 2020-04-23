using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver.Models;
using Aderant.Build.Logging;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyResolver.Resolvers {
    internal class NupkgResolver : IDependencyResolver {

        private ILogger logger;
        public bool EnableVerboseLogging { get; set; }

        static NupkgResolver() {
            // Paket used to support being loaded as byte array but current versions have a hard dependency on Assembly.Location which is
            // null when dynamically loaded from a byte array.
            var data = AppDomain.CurrentDomain.GetData("BuildScriptsDirectory") as string;
            if (!string.IsNullOrEmpty(data)) {
                Assembly.LoadFrom(Path.Combine(data, "paket.exe"));
            }
        }

        public NupkgResolver() {
        }

        public IModuleProvider ModuleFactory { get; set; }

        public IEnumerable<IDependencyRequirement> GetDependencyRequirements(ResolverRequest resolverRequest, ExpertModule module) {
            logger = resolverRequest.Logger;
            logger.Info("Calculating dependency requirements for {0}", module.Name);

            string moduleDirectory = resolverRequest.GetModuleDirectory(module);

            if (!string.IsNullOrEmpty(moduleDirectory)) {
                using (var manager = new PaketPackageManager(moduleDirectory, new PhysicalFileSystem(), WellKnownPackageSources.Default, logger, EnableVerboseLogging)) {
                    IEnumerable<string> groupList = manager.FindGroups();

                    foreach (string groupName in groupList) {
                        DependencyGroup dependencyGroup = manager.GetDependencies(groupName);

                        if (dependencyGroup.FrameworkRestrictions != null) {
                            foreach (string restriction in dependencyGroup.FrameworkRestrictions) {
                                resolverRequest.AddFrameworkRestriction(groupName, restriction);
                            }
                        }

                        foreach (var item in dependencyGroup.Requirements) {
                            var requirement = DependencyRequirement.Create(item.Key, groupName, item.Value);

                            var requirementWithGroupSupport = requirement as IDependencyGroup;
                            if (requirementWithGroupSupport != null) {
                                requirementWithGroupSupport.DependencyGroup = dependencyGroup;
                            }

                            bool replicationEnabled = true;
                            if (ReplicationExplicitlyDisabled.HasValue) {
                                if (ReplicationExplicitlyDisabled.Value) {
                                    replicationEnabled = false;
                                }
                            }

                            if (replicationEnabled) {
                                SetReplicationFlag(resolverRequest, requirement);
                            }

                            ReplaceVersionConstraint(resolverRequest, requirement);

                            yield return requirement;
                        }
                    }
                }
            }
        }

        public void Resolve(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken = default(CancellationToken)) {
            if (!requirements.Any()) {
                return;
            }

            logger = resolverRequest.Logger;
            logger.Info("Resolving packages...", null);

            if (resolverRequest.Modules.Count() == 1) {
                SingleModuleRestore(resolverRequest, requirements, cancellationToken);
            } else {
                ServerBuildRestore(resolverRequest, requirements, cancellationToken);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether replication explicitly disabled.
        /// Modules can tell the build system to not replicate packages to the dependencies folder via DependencyReplication=false
        /// in the DependencyManifest.xml
        /// </summary>
        public bool? ReplicationExplicitlyDisabled { get; set; }

        private static void SetReplicationFlag(ResolverRequest resolverRequest, IDependencyRequirement requirement) {
            if (resolverRequest.ModuleFactory != null) {
                var expertModule = resolverRequest.ModuleFactory.GetModule(requirement.Name);
                if (expertModule != null) {
                    if (expertModule.HasReplicateToDependenciesValue) {
                        requirement.ReplicateToDependencies = expertModule.ReplicateToDependencies;
                    } else {
                        requirement.ReplicateToDependencies = true;
                    }
                }
            }
        }

        private static void ReplaceVersionConstraint(ResolverRequest resolverRequest, IDependencyRequirement requirement) {
            var expertModule = resolverRequest.ModuleFactory?.GetModule(requirement.Name);
            if (expertModule != null) {
                if (expertModule.ReplaceVersionConstraint && expertModule.VersionRequirement != null) {
                    requirement.VersionRequirement = expertModule.VersionRequirement;
                    requirement.ReplaceVersionConstraint = true;
                }
            }
        }

        private void SingleModuleRestore(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken) {
            string directory = resolverRequest.GetDependenciesDirectory(requirements.First(), resolverRequest.ReplicationExplicitlyDisabled);

            var fs = new PhysicalFileSystem();
            PackageRestore(resolverRequest, directory, fs, requirements, cancellationToken);
        }

        private void ServerBuildRestore(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken) {
            var grouping = requirements.GroupBy(requirement => resolverRequest.GetDependenciesDirectory(requirement));

            foreach (var group in grouping) {
                logger.Info("Resolving packages for path: " + group.Key, null);

                PackageRestore(resolverRequest, group.Key, new PhysicalFileSystem(), group.ToList(), cancellationToken);
            }
        }

        private void PackageRestore(ResolverRequest resolverRequest, string directory, IFileSystem2 fileSystem, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken) {
            using (var manager = new PaketPackageManager(directory, fileSystem, WellKnownPackageSources.Default, logger, EnableVerboseLogging)) {
                manager.Add(requirements, resolverRequest);

                if (resolverRequest.Update) {
                    manager.Update(resolverRequest.Force);
                }

                manager.Restore(resolverRequest.Force);

                foreach (var requirement in requirements) {
                    cancellationToken.ThrowIfCancellationRequested();

                    resolverRequest.Resolved(requirement, this);

                    if (!ReplicationExplicitlyDisabled.GetValueOrDefault(false)) {
                            // here we override the global requires replication, if the individual package doesn't want to be replicated we honor that.
                        if (!requirement.ReplicateToDependencies) {
                            continue;
                        }

                        if (resolverRequest.RequiresThirdPartyReplication) {
                            if (resolverRequest.ReplicationExplicitlyDisabled) {
                                continue;
                            }

                            ReplicateToDependenciesDirectory(resolverRequest, directory, fileSystem, requirement);
                        }
                    }
                }
            }
        }
        
        // TODO: Remove this - now obsolete in 81+
        private void ReplicateToDependenciesDirectory(ResolverRequest resolverRequest, string directory, IFileSystem2 fileSystem, IDependencyRequirement requirement) {
            // For a build all we place the packages folder under dependencies
            // For a single module, it goes next to the dependencies folder
            if (requirement.Group == "Development") {
                return;
            }

            string packageDir = GeneratePathToPackage(directory, requirement);
            if (!fileSystem.DirectoryExists(packageDir)) {
                throw new DirectoryNotFoundException($"{packageDir} does not exist.");
            }

            string target = resolverRequest.GetDependenciesDirectory(requirement);

            foreach (string dir in fileSystem.GetDirectories(packageDir)) {
                if (dir.IndexOf("\\lib", StringComparison.OrdinalIgnoreCase) >= 0) {

                    foreach (string zipPath in fileSystem.GetFiles(dir, "Web.*.zip", true).Where(f => !f.EndsWith("dependencies.zip"))) {
                        logger.Info("Extracting web package archive {0}", zipPath);
                        var fs = new WebArchiveFileSystem(dir);
                        fs.ExtractArchive(zipPath, dir);
                    }

                    if (requirement.Name.IsOneOf(ModuleType.ThirdParty, ModuleType.Web)) {
                        logger.Info("Replicating {0} to {1}", dir, target);

                        // We need to do some "drafting" on the target path for Web module dependencies - a different destination path is
                        // used depending on the content type.
                        var selector = new WebContentDestinationRule(requirement, target);
                        FileSystem.DirectoryCopyAsync(dir, target, selector.GetDestinationForFile, true, false).Wait();
                        return;
                    }

                    logger.Info("Replicating {0} to {1}", dir, target);
                    fileSystem.CopyDirectory(dir, target);
                }
            }
        }

        private static string GeneratePathToPackage(string directory, IDependencyRequirement requirement) {
            var path = string.Equals(requirement.Group, Constants.MainDependencyGroup, StringComparison.OrdinalIgnoreCase) ? "" : requirement.Group;
            return Path.Combine(directory, "packages", path, requirement.Name);
        }

        public static void Initialize() {
        }
    }
}
