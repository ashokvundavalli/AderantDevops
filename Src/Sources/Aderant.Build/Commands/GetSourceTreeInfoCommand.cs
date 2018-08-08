using System.Management.Automation;
using Aderant.Build.VersionControl;

namespace Aderant.Build.Commands {
    [OutputType(typeof(SourceTreeInfo))]
    [Cmdlet(VerbsCommon.Get, "SourceTreeInfo")]
    public class GetSourceTreeInfoCommand : PSCmdlet {

        [Parameter]
        public string SourceDirectory { get; set; }

        [Parameter(Mandatory = true)]
        public string SourceBranch { get; set; }

        [Parameter(Mandatory = true)]
        public string TargetBranch { get; set; }

        [Parameter]
        public bool IsPullRequest { get; set; }

        protected override void ProcessRecord() {
            if (SourceDirectory == null) {
                SourceDirectory = this.SessionState.Path.CurrentFileSystemLocation.Path;
            }
            
            WriteDebug(LibGit2Sharp.GlobalSettings.NativeLibraryPath);

            var gvc = new GitVersionControl();
            var sourceInfo = gvc.GetChangesBetween(SourceDirectory, SourceBranch, TargetBranch);

            WriteObject(sourceInfo);
        }
    }
}
