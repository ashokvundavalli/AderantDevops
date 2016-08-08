using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;

namespace Aderant.Build {
    internal sealed class ModuleDependencyResolver {
        private readonly ExpertManifest expertManifest;
        private readonly ILogger logger;
        private IList<string> modulesInBuild;

        /// <summary>
        /// Occurs when a module dependency is copied.
        /// </summary>
        public event EventHandler<DependencyResolvedEventArgs> ModuleDependencyResolved;

        /// <summary>
        /// Gets the dependency sources.
        /// </summary>
        /// <value>
        /// The dependency sources.
        /// </value>
        public DependencySources DependencySources { get; private set; }

        /// <summary>
        /// Gets or sets the modules in build.
        /// </summary>
        /// <param name="value">
        ///     The modules in build.
        /// </param>
        public void SetModulesInBuild(IEnumerable<string> value) {
            if (value != null) {
                modulesInBuild = value.ToList();
            }
        }

        /// <summary>
        /// Gets or sets the name of the module currently in context.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public string ModuleName { get; set; }

        public bool Update { get; set; }
        public bool Force { get; set; }
        public bool Outdated { get; set; }
        internal bool BuildAll { get; set; }

        private void OnModuleDependencyResolved(DependencyResolvedEventArgs e) {
            EventHandler<DependencyResolvedEventArgs> handler = ModuleDependencyResolved;
            if (handler != null) {
                handler(this, e);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleDependencyResolver" /> class.
        /// </summary>
        /// <param name="expertManifest">The expert manifest.</param>
        /// <param name="dropPath">The drop path.</param>
        /// <param name="logger">The logger.</param>
        public ModuleDependencyResolver(ExpertManifest expertManifest, string dropPath, ILogger logger)
            : this(dropPath) {
            this.expertManifest = expertManifest;
            this.logger = logger;
        }

        private ModuleDependencyResolver(string dropPath) {
            DependencySources = new DependencySources(dropPath);
        }

        /// <summary>
        /// Copies the dependencies.
        /// </summary>
        /// <param name="dependenciesDirectory">The dependencies directory.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public async Task Resolve(string dependenciesDirectory, CancellationToken cancellationToken = default(CancellationToken)) {
            if (!Directory.Exists(dependenciesDirectory)) {
                Directory.CreateDirectory(dependenciesDirectory);
            }

            IEnumerable<ExpertModule> referencedModules;
            if (expertManifest.HasDependencyManifests) {
                referencedModules = expertManifest.DependencyManifests
                    .SelectMany(s => s.ReferencedModules)
                    .Distinct();
            } else {
                referencedModules = Enumerable.Empty<ExpertModule>();
            }

            // Remove the modules from the dependency tree that we are currently building. This is done as the dependencies don't need to
            // come from the drop but instead they will be produced by this build
            if (modulesInBuild != null) {
                referencedModules = GetReferencedModulesForBuild(referencedModules);
            }

            referencedModules = PostProcess(referencedModules);

            string moduleDirectory = Path.GetFullPath(Path.Combine(dependenciesDirectory, @"..\"));

            var fileSystem = new PhysicalFileSystem(moduleDirectory);

            await PackageRestore(fileSystem, referencedModules);

            if (Outdated) {
                // Return early if we are showing outdated packages as we don't want to "gd" over the dependencies
                return;
            }

            await LegacyGetDependencies(dependenciesDirectory, cancellationToken, referencedModules.Where(m => m.RepositoryType == RepositoryType.Folder));
        }

        private async Task PackageRestore(IFileSystem2 fileSystem2, IEnumerable<ExpertModule> referencedModules) {
            using (var manager = new PackageManager(fileSystem2, logger)) {
                manager.Add(new DependencyFetchContext(), referencedModules.Where(m => m.RepositoryType == RepositoryType.NuGet));

                if (Outdated) {
                    await manager.ShowOutdated();
                    return;
                }

                if (Update) {
                    logger.Info("Updating packages.");
                    await manager.Update(Force);
                }

                logger.Info("Restoring packages.");
                await manager.Restore();
            }

            await RestorePackages(fileSystem2, referencedModules);
        }

        private IEnumerable<ExpertModule> PostProcess(IEnumerable<ExpertModule> referencedModules) {
            var packagedModules = new string[] {"Aderant.Build.Analyzer"};

            foreach (var module in referencedModules) {
                if (module.ModuleType == ModuleType.ThirdParty || module.GetAction == GetAction.NuGet) {
                    module.RepositoryType = RepositoryType.NuGet;
                    continue;
                }

                foreach (var name in packagedModules) {
                    if (string.Equals(module.Name, name, StringComparison.OrdinalIgnoreCase)) {
                        module.RepositoryType = RepositoryType.NuGet;
                    }
                }
            }

            return referencedModules;
        }

        private async Task LegacyGetDependencies(string dependenciesDirectory, CancellationToken cancellationToken, IEnumerable<ExpertModule> referencedModules) {
            foreach (var referencedModule in referencedModules.OrderBy(m => m.Name)) {
                // Check if we have been issued CTRL + C from the command line, if so abort.
                if (cancellationToken != null) {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                string latestBuild = null;
                try {
                    latestBuild = expertManifest.GetPathToBinaries(referencedModule, DependencySources.DropLocation);
                } catch (BuildNotFoundException) {
                    string localModuleDirectory = Path.Combine(dependenciesDirectory, @"..\..\" + referencedModule, "bin", "module");
                    localModuleDirectory = Path.GetFullPath(localModuleDirectory);
                    latestBuild = localModuleDirectory;
                }
                if (Directory.Exists(latestBuild)) {
                    await CopyContentsAsync(dependenciesDirectory, referencedModule, latestBuild, false);
                }

                OnModuleDependencyResolved(new DependencyResolvedEventArgs {
                    DependencyProvider = referencedModule.Name,
                    Branch = Aderant.Build.Providers.PathHelper.GetBranch(latestBuild, false),
                    ResolvedUsingHardlink = false,
                    FullPath = latestBuild,
                });
            }
        }

        private async Task RestorePackages(IFileSystem2 fileSystem, IEnumerable<ExpertModule> referencedModules) {
            logger.Info("Performing legacy restore.");

            string moduleName = ModuleName ?? string.Empty;

            string dependenciesDirectory = Path.Combine(fileSystem.Root, "Dependencies");
            string packagesDirectory = Path.Combine(fileSystem.Root, "packages");

            if (!fileSystem.DirectoryExists(packagesDirectory)) {
                logger.Warning("Not packages directory at: {0}. This is probably an error as all modules should have dependencies. Continuing. ", packagesDirectory);
                return;
            }

            foreach (var package in Directory.EnumerateDirectories(packagesDirectory)) {
                string binariesDirectory = Path.Combine(package, "lib");
                var referencedModuleName = package.Split('\\').Last();
                var referencedModule = referencedModules.FirstOrDefault(m => m.Name.Equals(referencedModuleName, StringComparison.InvariantCultureIgnoreCase));

                if (referencedModule?.ModuleType != ModuleType.ThirdParty) {
                    continue;
                }

                logger.Info("Performing legacy restore on " + package);

                if (BuildAll || moduleName.StartsWith("Web.", StringComparison.OrdinalIgnoreCase) || moduleName.StartsWith("Mobile.", StringComparison.OrdinalIgnoreCase)) {
                    // We need to do some "drafting" on the target path for Web module dependencies - a different destination path is
                    // used depending on the content type.

                    var selector = new WebContentDestinationRule(referencedModule, dependenciesDirectory);
                    await FileSystem.DirectoryCopyAsync(binariesDirectory, dependenciesDirectory, selector.GetDestinationForFile, recursive: true, useHardLinks: false);
                } else {
                    await FileSystem.DirectoryCopyAsync(binariesDirectory, dependenciesDirectory, GetDependencyCopyHelper.DestinationSelector, recursive: true, useHardLinks: false);
                }

                referencedModule.Deploy(dependenciesDirectory);
            }
        }

        private IEnumerable<ExpertModule> GetReferencedModulesForBuild(IEnumerable<ExpertModule> referencedModules) {
            var builder = new DependencyBuilder(expertManifest);
            var modules = builder.GetAllModules().ToList();
            var moduleDependencyGraph = builder.GetModuleDependencies().ToList();

            var modulesRequiredForBuild = GetDependenciesRequiredForBuild(modules, moduleDependencyGraph, modulesInBuild);

            if (modulesRequiredForBuild.Count == 0) {
                // We don't require any external dependencies to build - however we need to move the third party modules to the dependency folder
                // always
                referencedModules = referencedModules.Where(m => m.ModuleType == ModuleType.ThirdParty);
            } else {
                referencedModules = modulesRequiredForBuild.Concat(referencedModules.Where(m => m.ModuleType == ModuleType.ThirdParty));
            }
            return referencedModules;
        }

        internal ICollection<ExpertModule> GetDependenciesRequiredForBuild(List<ExpertModule> modules, List<ModuleDependency> moduleDependencyGraph, IList<string> moduleNamesInBuild) {
            // This unique set of modules we need to build the current build queue.
            var dependenciesRequiredForBuild = new HashSet<ExpertModule>();

            var modulesInBuild = new HashSet<string>(moduleNamesInBuild, StringComparer.OrdinalIgnoreCase);

            foreach (var name in modulesInBuild) {
                ExpertModule module = null;
                try {
                    module = modules.Single(expertModule => string.Equals(expertModule.Name, name, StringComparison.OrdinalIgnoreCase));
                } catch (InvalidOperationException) {
                    throw new InvalidOperationException("Module: " + name + " does not exist in the Expert Manifest.");
                }

                IEnumerable<ExpertModule> dependenciesRequiredForModule = moduleDependencyGraph
                    .Where(dependency => dependency.Consumer.Equals(module)) // Find the module in the dependency graph
                    .Select(dependency => dependency.Provider);

                foreach (var dependency in dependenciesRequiredForModule) {
                    // Don't add the self pointer (Module1 <==> Module1)
                    if (!string.Equals(dependency.Name, name, StringComparison.OrdinalIgnoreCase)) {
                        // Test if the current build set contains the dependency - if it does we will be building the dependency
                        // rather than getting it from the drop
                        if (!modulesInBuild.Contains(dependency.Name)) {
                            dependenciesRequiredForBuild.Add(dependency);
                        }
                    }
                }
            }

            return dependenciesRequiredForBuild;
        }

        private async Task CopyContentsAsync(string moduleDependenciesDirectory, ExpertModule referencedModule, string latestBuildPath, bool useHardLinks) {
            DirectoryInfo directory = new DirectoryInfo(moduleDependenciesDirectory);

            if (directory.Parent == null) {
                throw new ArgumentOutOfRangeException(string.Format("Dependency path {0} does not refer to a valid Expert Module", moduleDependenciesDirectory ?? string.Empty));
            }

            string moduleName = ModuleName ?? string.Empty;

            Task task;

            if (referencedModule.ModuleType == ModuleType.ThirdParty && (moduleName.StartsWith("Web.", StringComparison.OrdinalIgnoreCase) || moduleName.StartsWith("Mobile.", StringComparison.OrdinalIgnoreCase))) {
                // We need to do some "drafting" on the target path for Web module dependencies - a different destination path is
                // used depending on the content type.

                var selector = new WebContentDestinationRule(referencedModule, moduleDependenciesDirectory);
                task = FileSystem.DirectoryCopyAsync(latestBuildPath, moduleDependenciesDirectory, selector.GetDestinationForFile, true, useHardLinks);
            } else {
                task = FileSystem.DirectoryCopyAsync(latestBuildPath, moduleDependenciesDirectory, GetDependencyCopyHelper.DestinationSelector, true, useHardLinks);
            }

            await task.ContinueWith(t => {
                if (t.Exception == null) {
                    referencedModule.Deploy(moduleDependenciesDirectory);
                } else {
                    throw t.Exception;
                }
            });
        }

        private class WebContentDestinationRule {
            private static readonly char[] directorySeparatorCharArray = new[] {Path.DirectorySeparatorChar};
            private readonly ExpertModule module;
            private readonly string moduleDependenciesDirectory;

            public WebContentDestinationRule(ExpertModule module, string moduleDependenciesDirectory) {
                this.module = module;
                this.moduleDependenciesDirectory = moduleDependenciesDirectory;
            }

            public string GetDestinationForFile(FileInfo[] files, FileInfo file) {
                // If this is web module content item it needs to go to Modules\Web.Expenses\Dependencies\ThirdParty.Foo
                string fileName = file.FullName;

                if (CreateInThirdPartyFolder(fileName)) {
                    int pos = fileName.IndexOf(moduleDependenciesDirectory, StringComparison.OrdinalIgnoreCase);

                    if (pos >= 0) {
                        string relativeDirectory = fileName.Substring(pos + moduleDependenciesDirectory.Length).TrimStart(directorySeparatorCharArray);
                        string destination = Path.Combine(moduleDependenciesDirectory, module.Name, relativeDirectory);

                        return destination;
                    }
                }

                if (GetDependencyCopyHelper.CommonExcludeFilesFromCopy(files, file)) {
                    return null;
                }

                // Otherwise go to Modules\Web.Expenses\Dependencies
                return file.FullName;
            }

            private static bool CreateInThirdPartyFolder(string file) {
                string extension = Path.GetExtension(file).ToLowerInvariant();

                switch (extension) {
                    case ".js":
                    case ".ts":
                    case ".css":
                    case ".less":
                    case ".png":
                    case ".gif":
                        return true;
                }

                return false;
            }
        }

        private static class GetDependencyCopyHelper {
            private static string[] portableExecutableExtensions = {".dll", ".exe"};

            private static string[] extensions = {
                // Exclude documentation files
                ".chm",

                // Exclude license attribution text files
                "license.txt",
                "License.rtf",
                "License.pdf",
                "Redist.txt",

                // Exclude configuration files
                "exe.config",
                "web.config",
                ".dll.config"
            };

            internal static string DestinationSelector(FileInfo[] files, FileInfo file) {
                // We don't want to copy down these as they don't provide any benefit locally
                if (file.FullName.IndexOf(@"Dependencies\Customization", StringComparison.OrdinalIgnoreCase) >= 0) {
                    if (file.FullName.EndsWith(".zip")) {
                        return null;
                    }
                }

                if (file.FullName.IndexOf(@"Dependencies\CodeAnalysis", StringComparison.OrdinalIgnoreCase) >= 0) {
                    return null;
                }

                if (CommonExcludeFilesFromCopy(files, file)) {
                    return null;
                }

                return file.FullName;
            }


            internal static bool CommonExcludeFilesFromCopy(FileInfo[] directoryContents, FileInfo file) {
                foreach (var extension in extensions) {
                    if (file.FullName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }

                // Exclude XML documentation files
                if (file.FullName.EndsWith(@".xml", StringComparison.OrdinalIgnoreCase)) {
                    foreach (var extension in portableExecutableExtensions) {
                        if (MatchByExtension(directoryContents, file, extension)) {
                            return true;
                        }
                    }
                }

                // Exclude PDB files
                if (file.FullName.EndsWith(@".pdb", StringComparison.OrdinalIgnoreCase)) {
                    foreach (var extension in portableExecutableExtensions) {
                        if (MatchByExtension(directoryContents, file, extension)) {
                            return true;
                        }
                    }
                }

                return false;
            }

            private static bool MatchByExtension(FileInfo[] files, FileInfo file, string extension) {
                // Do we have a matching file with another extension?
                string assembly = Path.ChangeExtension(file.Name, extension);
                foreach (FileInfo f in files) {
                    if (string.Equals(f.Name, assembly, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    internal class DependencyFetchContext : IPackageContext {
        public bool IncludeDevelopmentDependencies {
            get { return true; } 
        }

        public bool AllowExternalPackages {
            get { return false; }
        }
    }

    internal enum RepositoryType {
        Folder,
        NuGet
    }
}