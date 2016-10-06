using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyResolver.Resolvers {
    internal class ExpertModuleResolver : IDependencyResolver {
        private readonly IFileSystem2 fileSystem;
        private List<DependencySource> sources = new List<DependencySource>();

        public string Root { get; set; }

        public ExpertModuleResolver(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
            this.Root = fileSystem.Root;
            this.ManifestFinder = LoadManifestFromFile;
        }

        public FolderDependencySystem FolderDependencySystem { get; set; }

        private Stream LoadManifestFromFile(string name) {
            string modulePath = Root;

            if (!Root.TrimEnd(Path.DirectorySeparatorChar).EndsWith(name, StringComparison.OrdinalIgnoreCase)) {
                modulePath = Path.Combine(Root, name);
            }

            if (fileSystem.DirectoryExists(modulePath)) {
                var manifestFile = fileSystem.GetFiles(modulePath, DependencyManifest.DependencyManifestFileName, true).FirstOrDefault();

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
        public static string DropLocation {
            get { return "Drop"; }
        }

        public IEnumerable<IDependencyRequirement> GetDependencyRequirements(ResolverRequest resolverRequest, ExpertModule module) {
            Stream stream = ManifestFinder(module.Name);

            if (stream != null) {
                DependencyManifest manifest;
                using (stream) {
                    manifest = new DependencyManifest(module.Name, stream);
                }

                manifest.GlobalAttributesProvider = ModuleFactory as IGlobalAttributesProvider;

                foreach (var reference in manifest.ReferencedModules) {
                    yield return DependencyRequirement.Create(reference);
                }
            }
        }

        public void Resolve(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken = default(CancellationToken)) {
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

        protected virtual string TryGetBinariesPath(string resolverRequestDropPath, IDependencyRequirement requirement) {
            if (FolderDependencySystem == null) {
                FolderDependencySystem = new FolderDependencySystem(new PhysicalFileSystem(resolverRequestDropPath));
            }

            return FolderDependencySystem.GetBinariesPath(resolverRequestDropPath, requirement);
        }

        public void AddDependencySource(string dropPath, string type) {
            sources.Add(new DependencySource(dropPath, type));
        }
    }
}