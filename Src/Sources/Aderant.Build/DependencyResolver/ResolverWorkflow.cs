using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver.Resolvers;
using Aderant.Build.IO;
using Aderant.Build.Logging;
using Aderant.Build.Utilities;

namespace Aderant.Build.DependencyResolver {
    internal class ResolverWorkflow {
        private readonly ILogger logger;
        private readonly IFileSystem2 fileSystem;
        private List<string> enabledResolvers = new List<string> { nameof(NupkgResolver )};

        private ResolverRequest request;
        private ExpertManifest productManifest;

        public ResolverWorkflow(ILogger logger, IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
            this.logger = logger;
        }

        public bool Force { get; set; }

        /// <summary>
        /// The URI for dependency content. Expected to be a file system path.
        /// </summary>
        public string DropPath { get; set; }

        private ResolverRequest Request {
            get {
                if (request == null) {
                    request = new ResolverRequest(logger);
                }

                return request;
            }
        }

        public string ModulesRootPath { get; set; }

        public string ManifestFile { get; set; }

        /// <summary>
        /// Gets or sets the directory to place the dependencies into.
        /// </summary>
        /// <value>The dependencies directory.</value>
        public string DependenciesDirectory { get; set; }

        public IEnumerable<string> DirectoriesInBuild { get; private set; } = Enumerable.Empty<string>();

        internal List<string> EnabledResolvers {
            get { return enabledResolvers; }
        }

        /// <summary>
        /// Runs the resolver workflow.
        /// </summary>
        /// <param name="update">Determines if new versions of packages should be searched for.</param>
        /// <param name="enableVerboseLogging">Emit verbose details from the resolver.</param>
        /// <param name="cancellationToken">Allows the operation the be cancelled if necessary.</param>
        public void Run(bool update, bool enableVerboseLogging, CancellationToken cancellationToken = default(CancellationToken)) {
            if (productManifest != null) {
                productManifest.ModulesDirectory = ModulesRootPath;
            }

            Request.Force = Force;
            Request.Update = update;

            EnsureDestinationDirectoryFullPath();

            if (!string.IsNullOrWhiteSpace(DependenciesDirectory)) {
                Request.SetDependenciesDirectory(DependenciesDirectory);

                EnsureSymlinks();

                if (Force) {
                    // Work around for extraction bug that does not specify overwrite.
                    RemovePaketFiles(DependenciesDirectory);
                }
            } else {
                if (ModulesRootPath != null && DirectoriesInBuild.Count() == 1) {
                    Request.SetDependenciesDirectory(ModulesRootPath);
                }
            }

            var resolvers = new List<IDependencyResolver>();

            if (IncludeResolver(nameof(ExpertModuleResolver))) {
                ExpertModuleResolver moduleResolver;
                var resolverFileSystem = new PhysicalFileSystem(ModulesRootPath, logger);

                if (!string.IsNullOrWhiteSpace(ManifestFile)) {
                    moduleResolver = new ExpertModuleResolver(resolverFileSystem, ManifestFile);
                    Request.RequiresThirdPartyReplication = true;
                    Request.Force = true;
                } else {
                    moduleResolver = new ExpertModuleResolver(resolverFileSystem);
                }

                moduleResolver.AddDependencySource(DropPath, ExpertModuleResolver.DropLocation);
                resolvers.Add(moduleResolver);
            }

            if (IncludeResolver(nameof(NupkgResolver))) {
                resolvers.Add(new NupkgResolver());
            }

            var resolver = new Resolver(logger, resolvers.ToArray());
            resolver.ResolveDependencies(Request, enableVerboseLogging, cancellationToken);
        }

        private void RemovePaketFiles(string dependenciesDirectory) {
            fileSystem.DeleteDirectory(Path.Combine(dependenciesDirectory, "paket-files"), true);
        }

        private void EnsureSymlinks() {
            new DefaultSharedDependencyController(fileSystem).CreateLinks(DependenciesDirectory, DirectoriesInBuild);
        }

        private void EnsureDestinationDirectoryFullPath() {
            // Small issue here, the original definition of DependenciesDirectory in BranchConfig.xml assumes its relative to .git
            // So we have to blindly assume ModulesRootPath=.git
            if (DependenciesDirectory != null && !Path.IsPathRooted(DependenciesDirectory)) {
                DependenciesDirectory = Path.Combine(ModulesRootPath, DependenciesDirectory);
            }
        }

        private bool IncludeResolver(string name) {
            if (enabledResolvers != null && enabledResolvers.Count == 0) {
                return true;
            }

            var includeResolver = enabledResolvers != null && enabledResolvers.Contains(name);
            if (!includeResolver) {
                logger.Info($"Resolver '{name}' is not enabled.");
            }

            return includeResolver;
        }

        /// <summary>
        /// Sets the top level dependency manifest file
        /// </summary>
        public ResolverWorkflow WithProductManifest(string file) {
            if (file != null && fileSystem.FileExists(file)) {
                if (!string.IsNullOrWhiteSpace(file)) {
                    productManifest = ExpertManifest.Load(file);
                    Request.ModuleFactory = productManifest;
                }
            }

            return this;
        }

        public void WithConfiguration(XDocument configurationXml) {
            WithConfiguration(null, configurationXml);
        }

        /// <summary>
        /// Sets the configuration file used to control the resolver workflow.
        /// </summary>
        public ResolverWorkflow WithConfigurationFile(string file) {
            if (file != null && fileSystem.FileExists(file)) {
                if (!string.IsNullOrWhiteSpace(file)) {
                    using (var stream = fileSystem.OpenFile(file)) {
                        var configurationXml = XDocument.Load(stream);
                        WithConfiguration(file, configurationXml);

                        // Ensure the config file directory is added to the build.
                        SetDirectoriesInBuild(new string[] { Path.GetDirectoryName(file) });
                    }
                }
            }

            return this;
        }

        private void WithConfiguration(string file, XDocument configurationXml) {
            string sharedDependencyDirectory;
            ResolverSettingsReader.ReadResolverSettings(this, configurationXml, file, ModulesRootPath ?? string.Empty, out sharedDependencyDirectory);
            if (sharedDependencyDirectory != null) {
                DependenciesDirectory = sharedDependencyDirectory;
            }
        }

        public ResolverWorkflow WithRootPath(string path) {
            ModulesRootPath = path;
            return this;
        }

        /// <summary>
        /// Sets the directory to contributing to the build and thus the directories to scan for requirement information.
        /// </summary>
        public ResolverWorkflow WithDirectoriesInBuild(string currentDirectory, string root, params string[] additionalDirectories) {
            var scanner = new DirectoryScanner(fileSystem, logger);

            // Check the current directory first
            string directory = scanner.GetDirectoryNameOfFileAbove(currentDirectory, WellKnownPaths.EntryPointFilePath, new[] { root });
            if (directory != string.Empty) {
                SetDirectoriesInBuild(new[] { directory });
                return this;
            }

            if (additionalDirectories != null) {
                WithDirectoriesInBuild(new[] { root }.Concat(additionalDirectories));
            }

            return this;
        }

        /// <summary>
        /// Sets the directory to contributing to the build and thus the directories to scan for requirement information.
        /// </summary>
        public ResolverWorkflow WithDirectoriesInBuild(IEnumerable<string> directories) {
            var uniquePaths = directories.Distinct(StringComparer.OrdinalIgnoreCase);

            var scanner = new DirectoryScanner(fileSystem, logger);

            var contributors = new ConcurrentBag<string>();

            Parallel.ForEach(uniquePaths, root => {
                if (root != null) {
                    foreach (string file in scanner.TraverseDirectoriesAndFindFiles(root, new[] { WellKnownPaths.EntryPointFilePath }, maxDepth: 1)) {
                        contributors.Add(file.Replace(WellKnownPaths.EntryPointFilePath, string.Empty, StringComparison.OrdinalIgnoreCase));
                    }
                }
            });

            SetDirectoriesInBuild(contributors);

            return this;
        }

        private void SetDirectoriesInBuild(IEnumerable<string> contributors) {
            DirectoriesInBuild = DirectoriesInBuild.Union(contributors, PathComparer.Default);

            foreach (var contributor in contributors) {
                Request.AddModule(contributor);
            }
        }


        /// <summary>
        /// Sets a value indicating whether replication should be explicitly disabled.
        /// Modules can tell the build system to not replicate packages to the dependencies folder via DependencyReplication=false in the DependencyManifest.xml
        /// </summary>
        public ResolverWorkflow UseReplication(bool useReplication) {
            Request.ReplicationExplicitlyDisabled = !useReplication;
            return this;
        }

        /// <summary>
        /// Sets the directory to place the dependencies into.
        /// </summary>
        public ResolverWorkflow UseDependenciesDirectory(string dependenciesDirectory) {
            DependenciesDirectory = dependenciesDirectory;
            return this;
        }

        public ResolverWorkflow WithResolvers(params string[] resolvers) {
            this.enabledResolvers = resolvers.ToList();
            return this;
        }

        /// <summary>
        /// Provides access to the current resolver request for this instance.
        /// </summary>
        /// <returns></returns>
        internal ResolverRequest GetCurrentRequest() {
            return Request;
        }
    }
}
