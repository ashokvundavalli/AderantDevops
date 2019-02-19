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

        [Parameter(Mandatory = false, HelpMessage = "Specifies the SHA1 hashes to query in the cache.")]
        public string[] BucketIds { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "Specifies the build cache root URI (e.g. a directory path)")]
        public string DropLocation { get; set; }

        protected override void ProcessRecord() {
            cancellationTokenSource = new CancellationTokenSource();
            var service = new ArtifactService(new PowerShellLogger(Host));
            var metadata = service.GetBuildStateMetadata(BucketIds, DropLocation, cancellationTokenSource.Token);

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
