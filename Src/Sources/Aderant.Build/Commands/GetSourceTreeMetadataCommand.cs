using System.Management.Automation;
using System.Threading;
using Aderant.Build.Model;
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

        [Parameter (Mandatory = false, HelpMessage = "The commit to originate from.", ParameterSetName = "PatchBuilder")]
        [ValidateNotNullOrEmpty]
        public string SourceCommit { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Commits to exclude from update package content.", ParameterSetName = "PatchBuilder")]
        [ValidateNotNull]
        public string[] ExcludedCommits { get; set; }

        [Parameter(HelpMessage = "Indicates if uncommitted changes should be considered")]
        public SwitchParameter IncludeLocalChanges { get; set; }

        protected override void ProcessRecord() {
            if (SourceDirectory == null) {
                SourceDirectory = this.SessionState.Path.CurrentFileSystemLocation.Path;
            }

            WriteDebug(LibGit2Sharp.GlobalSettings.NativeLibraryPath);

            cts = new CancellationTokenSource();
            var gvc = new GitVersionControlService();

            SourceTreeMetadata sourceInfo;
            if (string.Equals("PatchBuilder", ParameterSetName)) {
                CommitConfiguration commitConfiguration = new CommitConfiguration(SourceCommit) {
                    ExcludedCommits = ExcludedCommits
                };

                sourceInfo = gvc.GetPatchMetadata(SourceDirectory, commitConfiguration, cts.Token);
            } else {
                sourceInfo = gvc.GetMetadata(SourceDirectory, SourceBranch, TargetBranch, IncludeLocalChanges, cts.Token);
            }

            WriteObject(sourceInfo);
        }

        protected override void StopProcessing() {
            cts.Cancel();

            base.StopProcessing();
        }
    }
}
