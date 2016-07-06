using System;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Task = System.Threading.Tasks.Task;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.Get, "ExpertDependenciesForModule")]
    public class GetExpertDependenciesForModule : PSCmdlet {
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

        protected override void ProcessRecord() {
            base.ProcessRecord();

            if (string.IsNullOrEmpty(ModuleName)) {
                ModuleName = ParameterHelper.GetCurrentModuleName(null, this.SessionState);
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

            string manifest = Path.GetFullPath(Path.Combine(BuildScriptsDirectory, @"..\Package\ExpertManifest.xml"));
            if (File.Exists(manifest)) {
                DependencyManifest dependencyManifest = DependencyManifest.LoadFromModule(moduleDirectory);
                
                ExpertManifest expertManifest = ExpertManifest.Load(manifest, new[] {dependencyManifest});

                Stopwatch sw = new Stopwatch();
                sw.Start();

                try {
                    Task.Run(async () => {
                        var resolver = new ModuleDependencyResolver(expertManifest, DropPath, new PowerShellLogger(Host));

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

                        await resolver.Resolve(moduleDependenciesDirectory, DependencyFetchMode.Default, BuildScriptsDirectory, CancellationToken.None);
                    }).Wait(); // Wait is used here as to not change the signature of the ProcessRecord method
                } catch (Exception ex) {
                    Host.UI.WriteErrorLine("Failed to get all module dependencies.");

                    AggregateException ae = ex as AggregateException;
                    if (ae != null) {
                        ae = ae.Flatten();

                        ae.Handle(exception => {
                            WriteError(new ErrorRecord(exception, "0", ErrorCategory.InvalidOperation, null));
                            return true;
                        });
                    }

                    sw.Stop();
                    sw = null;
                    return;
                }

                sw.Stop();

                // Only print the copy time if the copy was successful 
                Host.UI.WriteLine("Get dependencies completed in " + sw.Elapsed.ToString("mm\\:ss\\.ff"));
            }
        }
    }
}