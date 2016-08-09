using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Task = System.Threading.Tasks.Task;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.Get, "ExpertDependenciesForModule")]
    public class GetExpertDependenciesForModule : PSCmdlet {
        private CancellationTokenSource cancellationTokenSource;

        [Parameter(Mandatory = false, Position = 0)]
        public string ModuleName { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public string ModulesRootPath { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public string DropPath { get; set; }

        [Parameter(Mandatory = true, Position = 3)]
        public string BuildScriptsDirectory { get; set; }

        [Parameter(Mandatory = false, Position = 4)]
        public SwitchParameter Update { get; set; }

        [Parameter(Mandatory = false, Position = 5)]
        public SwitchParameter ShowOutdated { get; set; }

        [Parameter(Mandatory = false, Position = 6)]
        public SwitchParameter Force { get; set; }

        // This should be removed when we are fully migrated over. This exists for backwards compatibility with other versions of the build tools.
        [Parameter(Mandatory = false, Position = 7, DontShow = true)]
        [Obsolete]
        public SwitchParameter UseThirdPartyFromDrop { get; set; }

        [Parameter(Mandatory = false, Position = 8, HelpMessage = "Specifies the path the product manifest.")]
        public string ProductManifestPath { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            if (string.IsNullOrEmpty(ModuleName)) {
                ModuleName = ParameterHelper.GetCurrentModuleName(null, this.SessionState);
            }

            if (string.IsNullOrEmpty(ProductManifestPath)) {
                //ProductManifestPath = ParameterHelper.GetExpertManifestPath(ProductManifestPath, this.SessionState);
            }

            if (string.IsNullOrEmpty(BuildScriptsDirectory)) {
                BuildScriptsDirectory = Path.Combine(ParameterHelper.GetBranchModulesDirectory(null, this.SessionState), BuildInfrastructureHelper.PathToBuildScriptsFromModules);
            }

            if (string.IsNullOrEmpty(DropPath)) {
                DropPath = ParameterHelper.GetDropPath(null, SessionState);
            }

            string moduleDirectory = ModulesRootPath;
            // e.g Modules\Web.Expenses
            if (string.IsNullOrEmpty(ModulesRootPath)) {
                moduleDirectory = ParameterHelper.GetCurrentModulePath(ModuleName, SessionState);
            }

            // e.g Modules\Web.Expenses\Dependencies
            string moduleDependenciesDirectory = Path.Combine(moduleDirectory, "Dependencies");

            DependencyManifest dependencyManifest = DependencyManifest.LoadFromModule(moduleDirectory);

            IEnumerable<ExpertModule> availableModules;

            if (!string.IsNullOrEmpty(ProductManifestPath)) {
                ExpertManifest expertManifest = ExpertManifest.Load(ProductManifestPath, new[] { dependencyManifest });
                availableModules = expertManifest.DependencyManifests.SelectMany(s => s.ReferencedModules).Distinct();
            } else {
                availableModules = dependencyManifest.ReferencedModules;
            }

            Stopwatch sw = Stopwatch.StartNew();

            cancellationTokenSource = new CancellationTokenSource();

            try {
                var task = Task.Run(async () => {
                    var resolver = new ModuleDependencyResolver(availableModules, DropPath, new PowerShellLogger(Host));

                    resolver.ModuleName = ModuleName;
                    resolver.Update = Update.ToBool();
                    resolver.Outdated = ShowOutdated.ToBool();
                    resolver.Force = Force.ToBool();

                    resolver.ModuleDependencyResolved += (sender, args) => {
                        Host.UI.Write(ConsoleColor.Gray, Host.UI.RawUI.BackgroundColor, "Getting binaries for ");
                        Host.UI.Write(ConsoleColor.Green, Host.UI.RawUI.BackgroundColor, args.DependencyProvider);
                        Host.UI.Write(ConsoleColor.Gray, Host.UI.RawUI.BackgroundColor, " from the branch ");
                        Host.UI.Write(ConsoleColor.Green, Host.UI.RawUI.BackgroundColor, args.Branch);
                        Host.UI.WriteLine(ConsoleColor.Gray, Host.UI.RawUI.BackgroundColor, (args.ResolvedUsingHardlink ? " (local version)" : string.Empty));

                        Host.UI.WriteLine("Resolved path:" + args.FullPath);
                    };

                    await resolver.Resolve(moduleDependenciesDirectory, cancellationTokenSource.Token);
                });

                Task.WaitAll(task);

                Host.UI.WriteLine("Get dependencies completed in " + sw.Elapsed.ToString("mm\\:ss\\.ff"));
            } catch (AggregateException ex) {
                ex.Handle(e => {
                    TaskCanceledException tcex = e as TaskCanceledException;
                    if (tcex != null) {
                        Host.UI.WriteLine("Get dependencies aborted.");
                        return true;
                    }

                    Host.UI.WriteErrorLine("Failed to get all module dependencies.");
                    WriteError(new ErrorRecord(e, "0", ErrorCategory.InvalidArgument, null));

                    // Not handling any other types of exception.
                    return false;
                });
            } finally {
                sw.Stop();
            }
        }

        protected override void StopProcessing() {
            base.StopProcessing();
            cancellationTokenSource.Cancel();
        }
    }
}