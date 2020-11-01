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

        [Parameter(Mandatory = false, HelpMessage = "Release or Debug.")]
        [ValidateSet("Debug", "Release")]
        public string BuildFlavor { get; set; }

        protected override void ProcessRecord() {
            cancellationTokenSource = new CancellationTokenSource();
            var service = new StateFileService(new PowerShellLogger(this));

            var options = new BuildStateQueryOptions {
                BuildFlavor = BuildFlavor
            };
            var metadata = service.GetBuildStateMetadata(BucketIds, Tags, DropLocation, options, cancellationTokenSource.Token);

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
