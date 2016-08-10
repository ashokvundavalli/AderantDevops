using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    // TODO: Obsolete this and replace with PowerShell variant
    public class GetDependencies : Microsoft.Build.Utilities.Task, ICancelableTask {
        private CancellationTokenSource cancellationToken;

        [Required]
        public string ModulesRootPath { get; set; }

        [Required]
        public string DropPath { get; set; }

        [Required]
        public string ProductManifest { get; set; }

        /// <summary>
        /// Gets or sets the name of the module consuming the dependencies.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public string ModuleName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is running in the context of a build all.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [build all]; otherwise, <c>false</c>.
        /// </value>
        public bool BuildAll { get; set; }

        /// <summary>
        /// Gets or sets the modules in the build set.
        /// </summary>
        /// <value>
        /// The modules in build.
        /// </value>
        public ITaskItem[] ModulesInBuild { get; set; }

        /// <summary>
        /// Gets or sets the build scripts directory.
        /// </summary>
        /// <value>
        /// The build scripts directory.
        /// </value>
        [Required]
        public string BuildScriptsDirectory { get; set; }

        /// <summary>
        /// Gets or sets the TFS server.
        /// </summary>
        /// <value>
        /// The TFS server.
        /// </value>
        [Required]
        public string TeamFoundationServerUrl { get; set; }

        public string WorkspaceName { get; set; }

        public string WorkspaceOwner { get; set; }

        static GetDependencies() {
            VisualStudioEnvironmentContext.SetupContext();
        }

        public override bool Execute() {
            ModulesRootPath = Path.GetFullPath(ModulesRootPath);
            ProductManifest = Path.GetFullPath(ProductManifest);

            LogParameters();

            LogModulesInBuild();

            // e.g Modules\Web.Expenses\Dependencies
            string moduleDependenciesDirectory = Path.Combine(ModulesRootPath, "Dependencies");

            string manifest = Path.GetFullPath(ProductManifest);
            if (!File.Exists(manifest)) {
                throw new FileNotFoundException("Could not locate ExpertManifest at:", manifest);
            }

            Stopwatch sw = Stopwatch.StartNew();

            ModuleDependencyResolver resolver = CreateModuleResolver(ProductManifest);

            try {
                System.Threading.Tasks.Task.Run(async () => {
                    // Create a cancellation token so we can abort the async task
                    cancellationToken = new CancellationTokenSource();
                    await resolver.Resolve(moduleDependenciesDirectory, cancellationToken.Token);
                }).Wait(); // Wait is used here as to not change the signature of the Execute method
            } catch (Exception ex) {
                Log.LogError("Failed to get all module dependencies.", null);

                AggregateException ae = ex as AggregateException;
                if (ae != null) {
                    ae = ae.Flatten();

                    ae.Handle(exception => {
                        Log.LogError(exception.Message, null);
                        Log.LogErrorFromException(exception, true);
                        return true;
                    });
                }

                return false;
            } finally {
                sw.Stop();
            }

            // Only print the copy time if the copy was successful 
            Log.LogMessage("Get dependencies completed in " + sw.Elapsed.ToString("mm\\:ss\\.ff"), null);

            return !Log.HasLoggedErrors;
        }

        private void LogParameters() {
            Log.LogMessage(MessageImportance.Normal, "ModulesRootPath: " + ModulesRootPath, null);
            Log.LogMessage(MessageImportance.Normal, "DropPath: " + DropPath, null);
            Log.LogMessage(MessageImportance.Normal, "ProductManifest: " + ProductManifest, null);
        }

        private void LogModulesInBuild() {
            if (ModulesInBuild != null) {
                Log.LogMessage(new string('=', 40), null);

                Log.LogMessage("Modules in build:...", null);

                foreach (var taskItem in ModulesInBuild) {
                    Log.LogMessage(Path.GetFileName(taskItem.ItemSpec), null);
                }

                Log.LogMessage(new string('=', 40), null);
            }
        }

        private ModuleDependencyResolver CreateModuleResolver(string expertManifest) {
            var dependencyManifests = GetDependencyManifests();

            IEnumerable<ExpertModule> modules = ModuleDependencyResolver.BuildDependencyTree(expertManifest, dependencyManifests);

            var resolver = new ModuleDependencyResolver(modules, DropPath, new BuildTaskLogger(this));
            resolver.BuildAll = BuildAll;

            if (!string.IsNullOrEmpty(ModuleName)) {
                resolver.ModuleName = ModuleName;
                Log.LogMessage(MessageImportance.Normal, "Fetch modules for: " + resolver.ModuleName, null);
            }

            if (ModulesInBuild != null) {
                resolver.SetModulesInBuild(ModulesInBuild.Select(m => Path.GetFileName(Path.GetFullPath(m.ItemSpec))));
            }

            resolver.ModuleDependencyResolved += (sender, args) => Log.LogMessage(MessageImportance.Normal, "Getting binaries for {0} from the branch {1} {2}", args.DependencyProvider, args.Branch, (args.ResolvedUsingHardlink ? " (local version)" : string.Empty));
            return resolver;
        }

        /// <summary>
        /// Attempts to cancel this instance.
        /// </summary>
        public void Cancel() {
            if (cancellationToken != null) {
                // Signal the cancellation token that we want to abort the async task
                cancellationToken.Cancel();
            }
        }

        private IEnumerable<DependencyManifest> GetDependencyManifests() {
            IList<DependencyManifest> manifests;
            if (BuildAll) {
                manifests = DependencyManifest.LoadAll(ModulesRootPath);
            } else {
                DependencyManifest dependencyManifest = DependencyManifest.LoadFromModule(ModulesRootPath);
                manifests = new[] { dependencyManifest };
            }
            return manifests;
        }
    }
}