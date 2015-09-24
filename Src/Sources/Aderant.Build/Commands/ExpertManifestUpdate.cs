using System;
using System.ComponentModel;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Providers;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.New, "ExpertManifestForBranch")]
    [Description("Walks all modules in the current branch and adds the module reference information from the DependencyManifest to the ExpertManifest")]
    public class ExpertManifestUpdate : PSCmdlet {

        [Parameter(HelpMessage = "The branch to use as the parent of the Target Branch. If not specified it defaults to MAIN.", Position = 0)]
        public string SourceBranch {
            get;
            set;
        }

        [Parameter(HelpMessage = "The target branch. If not specified defaults to the current branch.", Position = 1)]
        public string TargetBranch {
            get;
            set;
        }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            if (string.IsNullOrEmpty(TargetBranch)) {
                TargetBranch = ParameterHelper.GetBranchName(SessionState);
            }

            if (string.IsNullOrEmpty(SourceBranch) && TargetBranch.StartsWith("Releases", StringComparison.OrdinalIgnoreCase)) {
                SourceBranch = TargetBranch;
            }

            if (string.IsNullOrEmpty(SourceBranch)) {
                SourceBranch = "Main";
            }

            Host.UI.WriteLine();
            Host.UI.WriteLine();
            Host.UI.WriteLine("Source Branch: " + SourceBranch);
            Host.UI.WriteLine("Target Branch: " + TargetBranch);
            
            string modulesDirectory = ParameterHelper.GetBranchModulesDirectory(TargetBranch, SessionState);

            ProductManifestUpdater updater = new ProductManifestUpdater(new PowerShellLogger(Host), ExpertManifest.Load(modulesDirectory));
            updater.Update(SourceBranch, TargetBranch);
        }
    }
}