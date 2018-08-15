using System.Management.Automation;
using Aderant.Build.VersionControl;

namespace Aderant.Build.Commands {
    [OutputType(typeof(SourceTreeMetadata))]
    [Cmdlet(VerbsCommon.Get, "SourceTreeMetadata")]
    public class GetSourceTreeMetadataCommand : PSCmdlet {

        [Parameter]
        public string SourceDirectory { get; set; }

        [Parameter]
        public string SourceBranch { get; set; }

        [Parameter]
        public string TargetBranch { get; set; }

        protected override void ProcessRecord() {
            if (SourceDirectory == null) {
                SourceDirectory = this.SessionState.Path.CurrentFileSystemLocation.Path;
            }
            
            WriteDebug(LibGit2Sharp.GlobalSettings.NativeLibraryPath);

            System.Diagnostics.Debugger.Launch();

            var gvc = new GitVersionControl();
            var sourceInfo = gvc.GetMetadata(SourceDirectory, SourceBranch, TargetBranch);

            WriteObject(sourceInfo);
        }
    }
}
