using System.Management.Automation;
using System.Threading;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.Commands {

    [Cmdlet(VerbsCommon.Get, "BuildStateMetadata")]
    [OutputType(typeof(BuildStateMetadata))]
    public class GetBuildStateMetadataCommand : PSCmdlet {
        private CancellationTokenSource cancellationTokenSource;

        [Parameter(Mandatory = true, HelpMessage = "Specifies the SHA1 hashes to query in the cache.")]
        [ValidateNotNullOrEmpty]
        public string[] BucketIds { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Specifies the metadata about the hash.")]
        public string[] Tags { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specifies the build cache root URI (e.g. a directory path)")]
        [ValidateNotNullOrEmpty]
        public string DropLocation { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "The source branch used to validate artifacts.")]
        [ValidateNotNullOrEmpty]
        public string ScmBranch { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "The target branch used to validate artifacts.")]
        public string TargetBranch { get; set; }

        protected override void ProcessRecord() {
            cancellationTokenSource = new CancellationTokenSource();
            var service = new ArtifactService(new PowerShellLogger(this));

            var metadata = service.GetBuildStateMetadata(BucketIds, Tags, DropLocation, ScmBranch, TargetBranch, cancellationTokenSource.Token);

            if (metadata.BuildStateFiles != null) {
                WriteInformation("Found " + metadata.BuildStateFiles.Count + " state files", null);
            }

            WriteObject(metadata);
        }
        protected override void StopProcessing() {
            cancellationTokenSource.Cancel();

            base.StopProcessing();
        }
    }
}
