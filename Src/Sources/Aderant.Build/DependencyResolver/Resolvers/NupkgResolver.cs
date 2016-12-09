using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyResolver.Resolvers {
    internal class NupkgResolver : IDependencyResolver {
        private ILogger logger;
        public IModuleProvider ModuleFactory { get; set; }

        public IEnumerable<IDependencyRequirement> GetDependencyRequirements(ResolverRequest resolverRequest, ExpertModule module) {
            logger = resolverRequest.Logger;
            logger.Info("Calculating dependency requirements for {0}", module.Name);

            string moduleDirectory = resolverRequest.GetModuleDirectory(module);

            if (!string.IsNullOrEmpty(moduleDirectory)) {
                using (var pm = new PackageManager(new PhysicalFileSystem(moduleDirectory), logger)) {
                    var requirements = pm.GetDependencies();

                    foreach (var item in requirements) {
                        yield return DependencyRequirement.Create(item.Key, item.Value);
                    }
                }
            }
        }

        public void Resolve(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken = default(CancellationToken)) {
            if (!requirements.Any()) {
                return;
            }

            logger = resolverRequest.Logger;
            logger.Info("Resolving packages...");

            if (resolverRequest.Modules.Count() == 1) {
                SingleModuleRestore(resolverRequest, requirements, cancellationToken);
            } else {
                ServerBuildRestore(resolverRequest, requirements, cancellationToken);
            }
        }

        private void SingleModuleRestore(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken) {
            string directory = resolverRequest.GetDependenciesDirectory(requirements.First());

            if (directory.IndexOf("Dependencies", StringComparison.OrdinalIgnoreCase) >= 0) {
                var fs = new PhysicalFileSystem(Path.GetDirectoryName(directory));

                PackageRestore(resolverRequest, fs, requirements, cancellationToken);
            }
        }

        private void ServerBuildRestore(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken) {
            var grouping = requirements.GroupBy(requirement => resolverRequest.GetDependenciesDirectory(requirement));

            foreach (var group in grouping) {
                logger.Info("Resolving packages for path: " + group.Key);

                PackageRestore(resolverRequest, new PhysicalFileSystem(group.Key), group.ToList(), cancellationToken);
            }
        }

        private void PackageRestore(ResolverRequest resolverRequest, IFileSystem2 fileSystem, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken) {
            using (var manager = new PackageManager(fileSystem, logger)) {
                manager.Add(new DependencyFetchContext(), requirements);
                if (resolverRequest.Update) {
                    manager.Update(resolverRequest.Force);
                }
                manager.Restore(resolverRequest.Force);
                
                foreach (var requirement in requirements) {
                    cancellationToken.ThrowIfCancellationRequested();

                    resolverRequest.Resolved(requirement, this);

                    if (resolverRequest.RequiresReplication) {
                        ReplicateToDependenciesDirectory(resolverRequest, fileSystem, requirement);
                    }
                }
            }
        }

        private void ReplicateToDependenciesDirectory(ResolverRequest resolverRequest, IFileSystem2 fileSystem, IDependencyRequirement requirement) {
            // For a build all we place the packages folder under dependencies
            // For a single module, it goes next to the dependencies folder
            string packageDir = Path.Combine(fileSystem.Root, "packages", requirement.Name);
            if (!fileSystem.DirectoryExists(packageDir)) {
                var javaScriptPackageDir = Path.Combine(fileSystem.Root, "packages", "javascript", requirement.Name);
                if (!fileSystem.DirectoryExists(javaScriptPackageDir)) {
                    throw new DirectoryNotFoundException($"Neither {packageDir} nor {javaScriptPackageDir} exist.");
                }
                packageDir = javaScriptPackageDir;
            }

            string target = resolverRequest.GetDependenciesDirectory(requirement);

            foreach (string dir in fileSystem.GetDirectories(packageDir)) {
                if (dir.IndexOf("\\lib", StringComparison.OrdinalIgnoreCase) >= 0) {
                    logger.Info("Replicating {0} to {1}", dir, target);

                    if (requirement.Name.IsOneOf(ModuleType.ThirdParty, ModuleType.Web)) {
                        // We need to do some "drafting" on the target path for Web module dependencies - a different destination path is
                        // used depending on the content type.
                        var selector = new WebContentDestinationRule(requirement, target);
                        FileSystem.DirectoryCopyAsync(fileSystem.GetFullPath(dir), target, selector.GetDestinationForFile, true, false).Wait();
                        return;
                    }

                    fileSystem.CopyDirectory(dir, target);
                }
            }
        }
    }
}