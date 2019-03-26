using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyResolver.Resolvers {
    public class ExpertModuleResolver : IDependencyResolver {

        private readonly IFileSystem2 fileSystem;
        private List<DependencySource> sources = new List<DependencySource>();

        public string Root { get; set; }

        public ExpertModuleResolver(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
            this.Root = fileSystem.Root;
            this.ManifestFinder = FindManifest;
        }

        public ExpertModuleResolver(IFileSystem2 fileSystem, string manifestFile) {
            this.fileSystem = fileSystem;
            this.Root = fileSystem.Root;
            ManifestFinder = s => fileSystem.OpenFile(manifestFile);
        }

        internal FolderDependencySystem FolderDependencySystem { get; set; }

        private Stream FindManifest(string path) {
            if (fileSystem.DirectoryExists(path)) {
                var manifestFile = fileSystem.GetFiles(path, DependencyManifest.DependencyManifestFileName, true).FirstOrDefault();

                if (manifestFile != null) {
                    return fileSystem.OpenFile(manifestFile);
                }
            }

            return null;
        }

        internal Func<string, Stream> ManifestFinder { get; set; }

        public IModuleProvider ModuleFactory { get; set; }

        /// <summary>
        /// Gets the drop location well known type.
        /// </summary>
        /// <value>The drop location.</value>
        public static string DropLocation => "Drop";

        IEnumerable<IDependencyRequirement> IDependencyResolver.GetDependencyRequirements(ResolverRequest resolverRequest, ExpertModule module) {
            string moduleDirectory = resolverRequest.GetModuleDirectory(module);
            resolverRequest.Logger.Info(string.Concat("Probing for DependencyManifest under: ", moduleDirectory));

            Stream stream = ManifestFinder(moduleDirectory);

            if (stream != null) {
                DependencyManifest manifest;
                using (stream) {
                    manifest = new DependencyManifest(module.Name, stream);
                }

                manifest.GlobalAttributesProvider = ModuleFactory as IGlobalAttributesProvider;

                bool? dependencyReplicationEnabled = manifest.DependencyReplicationEnabled;
                if (dependencyReplicationEnabled.HasValue) {
                    ReplicationExplicitlyDisabled = !dependencyReplicationEnabled.Value;
                }

                foreach (ExpertModule reference in manifest.ReferencedModules) {
                    var requirement = DependencyRequirement.Create(reference);
                    yield return requirement;
                }
            } else {
                resolverRequest.Logger.Info("No DependencyManifest found");
            }
        }

        void IDependencyResolver.Resolve(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken) {
            ResolveAll(resolverRequest, requirements, cancellationToken);
        }

        private void ResolveAll(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken) {
            if (!sources.Any()) {
                throw new InvalidOperationException("Cannot perform resolution with no sources. Call AddDependencySource with a source.");
            }

            foreach (var requirement in requirements) {
                cancellationToken.ThrowIfCancellationRequested();

                if (!(requirement is FolderBasedRequirement)) {
                    resolverRequest.Unresolved(requirement, this);
                    continue;
                }

                resolverRequest.Logger.Info("Resolving requirement: {0}", requirement.Name);

                if (!requirement.Name.IsOneOf(ModuleType.Help)) {
                    if (requirement.VersionRequirement.AssemblyVersion == null) {
                        // No assembly version means we cannot resolve this requirement
                        resolverRequest.Unresolved(requirement, this);

                        resolverRequest.Logger.Debug($"Requirement: {requirement.Name} could not be resolved by this resolver.");
                        continue;
                    }
                }

                bool resolved = false;
                foreach (DependencySource source in sources) {
                    string path = TryGetBinariesPath(source.Path, requirement);

                    if (path != null) {
                        resolverRequest.Logger.Info("Resolved requirement {0} to path {1}", requirement.Name, path);

                        CopyContents(resolverRequest.Logger, resolverRequest.GetDependenciesDirectory(requirement), requirement, path);

                        resolverRequest.Logger.Info($"Requirement: {requirement.Name} was resolved.");
                        resolved = true;
                        resolverRequest.Resolved(requirement, this);
                        break;
                    }
                }

                if (!resolved) {
                    resolverRequest.Unresolved(requirement, this);
                }
            }
        }

        private void CopyContents(ILogger resolverRequestLogger, string moduleDependenciesDirectory, IDependencyRequirement requirement, string latestBuildPath) {
            if (requirement.Name.IsOneOf(ModuleType.ThirdParty, ModuleType.Web)) {
                // We need to do some "drafting" on the target path for Web module dependencies - a different destination path is
                // used depending on the content type.
                var selector = new WebContentDestinationRule(requirement, moduleDependenciesDirectory);
                FileSystem.DirectoryCopyAsync(latestBuildPath, moduleDependenciesDirectory, selector.GetDestinationForFile, true, false).Wait();

                resolverRequestLogger.Info("Extracting web package archive");

                var fs = new WebArchiveFileSystem(moduleDependenciesDirectory);
                fs.ExtractArchive(Path.Combine(moduleDependenciesDirectory, requirement.Name + ".zip"), moduleDependenciesDirectory);

                return;
            }

            fileSystem.CopyDirectory(latestBuildPath, moduleDependenciesDirectory);
        }

        internal virtual string TryGetBinariesPath(string resolverRequestDropPath, IDependencyRequirement requirement) {
            if (FolderDependencySystem == null) {
                FolderDependencySystem = new FolderDependencySystem(new PhysicalFileSystem(resolverRequestDropPath));
            }

            return FolderDependencySystem.GetBinariesPath(resolverRequestDropPath, requirement);
        }

        public void AddDependencySource(string dropPath, string type) {
            sources.Add(new DependencySource(dropPath, type));
        }

        public bool? ReplicationExplicitlyDisabled { get; set; }
    }
}
