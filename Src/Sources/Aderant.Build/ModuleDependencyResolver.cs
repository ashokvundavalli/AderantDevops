using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;
using ModuleType = Aderant.Build.DependencyAnalyzer.ModuleType;

namespace Aderant.Build {
    internal sealed class ModuleDependencyResolver {
        private readonly ExpertManifest expertManifest;
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
        /// <value>
        /// The modules in build.
        /// </value>
        public IEnumerable<string> ModulesInBuild {
            get { return modulesInBuild; }
            set {
                if (value != null) {
                    modulesInBuild = value.ToList();
                }
            }
        }

        /// <summary>
        /// Gets or sets the name of the module currently in context.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public string ModuleName { get; set; }

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
        public ModuleDependencyResolver(ExpertManifest expertManifest, string dropPath)
            : this(dropPath) {
            this.expertManifest = expertManifest;
        }

        private ModuleDependencyResolver(string dropPath) {
            DependencySources = new DependencySources(dropPath);
        }

        /// <summary>
        /// Copies the dependencies.
        /// </summary>
        /// <param name="dependenciesDirectory">The dependencies directory.</param>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task CopyDependenciesFromDrop(string dependenciesDirectory, DependencyFetchMode mode, CancellationTokenSource cancellationToken = null) {
            IEnumerable<ExpertModule> referencedModules = expertManifest.DependencyManifests
                .SelectMany(s => s.ReferencedModules)
                .Distinct();

            // Remove the modules from the dependency tree that we are currently building. This is done as the dependencies don't need to
            // come from the drop but instead they will be produced by this build
            if (modulesInBuild != null) {
                referencedModules = GetReferencedModulesForBuild(mode, referencedModules);
            }

            foreach (var referencedModule in referencedModules.OrderBy(m => m.Name)) {
                // Check if we have been issued CTRL + C from the command line, if so abort.
                if (cancellationToken != null && cancellationToken.IsCancellationRequested) {
                    return;
                }

                string latestBuild = null;
                bool useHardLinks = false;

                // Optimization. If we have a local ThirdParty dependency source attempt to use it.
                if (!string.IsNullOrEmpty(DependencySources.LocalThirdPartyDirectory)) {
                    if (referencedModule.ModuleType == ModuleType.ThirdParty && string.IsNullOrEmpty(referencedModule.Branch)) {
                        // The third party module can come straight out of the TFS workspace - we don't need to copy it from the drop
                        try {
                            latestBuild = expertManifest.GetPathToBinaries(referencedModule, DependencySources.LocalThirdPartyDirectory);
                            useHardLinks = true;
                        } catch (BuildNotFoundException) {
                        }
                    }
                }

                if (string.IsNullOrEmpty(latestBuild)) {
                    useHardLinks = false;
                    latestBuild = expertManifest.GetPathToBinaries(referencedModule, DependencySources.DropLocation);
                }

                if (Directory.Exists(latestBuild)) {
                    await CopyContentsAsync(dependenciesDirectory, referencedModule, latestBuild, useHardLinks, mode);

                    OnModuleDependencyResolved(new DependencyResolvedEventArgs {
                        DependencyProvider = referencedModule.Name,
                        Branch = PathHelper.GetBranch(latestBuild),
                        ResolvedUsingHardlink = useHardLinks,
                        FullPath = latestBuild,
                    });
                }
            }
        }

        private IEnumerable<ExpertModule> GetReferencedModulesForBuild(DependencyFetchMode mode, IEnumerable<ExpertModule> referencedModules) {
            var builder = new DependencyBuilder(expertManifest);
            var modules = builder.GetAllModules().ToList();
            var moduleDependencyGraph = builder.GetModuleDependencies().ToList();

            var modulesRequiredForBuild = GetDependenciesRequiredForBuild(modules, moduleDependencyGraph, modulesInBuild);

            if (modulesRequiredForBuild.Count == 0 && mode == DependencyFetchMode.ThirdParty) {
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

        private async Task CopyContentsAsync(string moduleDependenciesDirectory, ExpertModule referencedModule, string latestBuildPath, bool useHardLinks, DependencyFetchMode mode) {
            DirectoryInfo directory = new DirectoryInfo(moduleDependenciesDirectory);

            if (directory.Parent == null) {
                throw new ArgumentOutOfRangeException(string.Format("Dependency path {0} does not refer to a valid Expert Module", moduleDependenciesDirectory ?? string.Empty));
            }

            string moduleName = ModuleName ?? string.Empty;
            
            Task task;

            if (referencedModule.ModuleType == ModuleType.ThirdParty && (moduleName.StartsWith("Web.", StringComparison.OrdinalIgnoreCase) || moduleName.StartsWith("Mobile.", StringComparison.OrdinalIgnoreCase)  || mode == DependencyFetchMode.ThirdParty)){
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

    internal class DependencySources {
        /// <summary>
        /// Initializes a new instance of the <see cref="DependencySources"/> class.
        /// </summary>
        /// <param name="dropPath">The drop path.</param>
        public DependencySources(string dropPath) {
            DropLocation = dropPath;
        }

        /// <summary>
        /// Gets or sets the third party module location.
        /// </summary>
        /// <value>
        /// The third party.
        /// </value>
        public string LocalThirdPartyDirectory { get; set; }

        /// <summary>
        /// Gets or sets the drop location.
        /// </summary>
        /// <value>
        /// The drop location.
        /// </value>
        public string DropLocation { get; private set; }

        internal static string GetLocalPathToThirdPartyBinaries(string tfsServerUri, string branchRoot) {
            if (!string.IsNullOrEmpty(branchRoot)) {
                SourceControl sourceControl = SourceControl.CreateFromBranchRoot(tfsServerUri, branchRoot);
                return sourceControl.BranchInfo.ThirdPartyFolder;
            }
            return null;
        }


    }
}