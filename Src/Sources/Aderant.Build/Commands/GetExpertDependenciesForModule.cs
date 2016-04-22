using System;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Providers;
using Microsoft.TeamFoundation.VersionControl.Client;
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

        [Parameter(Mandatory = false, Position = 3)]
        public string BuildScriptsDirectory { get; set; }

        [Parameter(Mandatory = false, Position = 3)]
        public SwitchParameter UseThirdPartyFromDrop { get; set; }

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

            // e.g Modules\Web.Expenses
            string moduleDirectory = ParameterHelper.GetCurrentModulePath(ModuleName, SessionState);

            // e.g Modules\Web.Expenses\Dependencies
            string moduleDependenciesDirectory = Path.Combine(moduleDirectory, "Dependencies");

            string branchRoot;
            ParameterHelper.TryGetBranchModulesDirectory(null, SessionState, out branchRoot);

            string manifest = Path.GetFullPath(Path.Combine(BuildScriptsDirectory, @"..\Package\ExpertManifest.xml"));
            if (File.Exists(manifest)) {
                DependencyManifest dependencyManifest = DependencyManifest.LoadFromModule(moduleDirectory);

                if (dependencyManifest.ReferencedModules.Count == 0) {
                    WriteDebug("There are no referenced modules");
                    return;
                }

                ExpertManifest expertManifest = ExpertManifest.Load(manifest, new[] { dependencyManifest });

                Stopwatch sw = new Stopwatch();
                sw.Start();

                try {
                    Task.Run(async () => {
                        var resolver = new ModuleDependencyResolver(expertManifest, DropPath);

                        if (!UseThirdPartyFromDrop) {
                            resolver.DependencySources.LocalThirdPartyDirectory = DependencySources.GetLocalPathToThirdPartyBinaries(null, branchRoot, null, null);
                        }

                        resolver.ModuleName = ModuleName;

                        Host.UI.WriteDebugLine("Using local thirdparty path: " + resolver.DependencySources.LocalThirdPartyDirectory);

                        resolver.ModuleDependencyResolved += (sender, args) => {
                            Host.UI.Write(ConsoleColor.Gray, Host.UI.RawUI.BackgroundColor, "Getting binaries for ");
                            Host.UI.Write(ConsoleColor.Green, Host.UI.RawUI.BackgroundColor, args.DependencyProvider);
                            Host.UI.Write(ConsoleColor.Gray, Host.UI.RawUI.BackgroundColor, " from the branch ");
                            Host.UI.Write(ConsoleColor.Green, Host.UI.RawUI.BackgroundColor, args.Branch);
                            Host.UI.WriteLine(ConsoleColor.Gray, Host.UI.RawUI.BackgroundColor, (args.ResolvedUsingHardlink ? " (local version)" : string.Empty));

                            Host.UI.WriteDebugLine("Resolved path:" + args.FullPath);
                        };

                        await resolver.CopyDependenciesFromDrop(moduleDependenciesDirectory, DependencyFetchMode.Default);
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