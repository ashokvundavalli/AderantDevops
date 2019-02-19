using System.Management.Automation;
using System.Threading;
using Aderant.Build.VersionControl;

namespace Aderant.Build.Commands {
    [OutputType(typeof(SourceTreeMetadata))]
    [Cmdlet(VerbsCommon.Get, "SourceTreeMetadata")]
    public class GetSourceTreeMetadataCommand : PSCmdlet {
        private CancellationTokenSource cts;

        [Parameter]
        public string SourceDirectory { get; set; }

        [Parameter]
        public string SourceBranch { get; set; }

        [Parameter]
        public string TargetBranch { get; set; }

        [Parameter(HelpMessage = "Indicates if uncommitted changes should be considered")]
        public SwitchParameter IncludeLocalChanges { get; set; }

        protected override void ProcessRecord() {
            if (SourceDirectory == null) {
                SourceDirectory = this.SessionState.Path.CurrentFileSystemLocation.Path;
            }

            WriteDebug(LibGit2Sharp.GlobalSettings.NativeLibraryPath);

            cts = new CancellationTokenSource();
            var gvc = new GitVersionControlService();
            var sourceInfo = gvc.GetMetadata(SourceDirectory, SourceBranch, TargetBranch, IncludeLocalChanges, cts.Token);

            WriteObject(sourceInfo);
        }

        protected override void StopProcessing() {
            cts.Cancel();

            base.StopProcessing();
        }
    }
}
