using System.Management.Automation;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.Commands {

    [Cmdlet(VerbsCommon.Get, "BuildStateMetadata")]
    [OutputType(typeof(BuildStateMetadata))]
    public class GetBuildStateMetadataCommand : PSCmdlet {

        [Parameter(Mandatory = true)]
        public string[] BucketIds { get; set; }

        [Parameter(Mandatory = true)]
        public string DropLocation { get; set; }

        protected override void ProcessRecord() {
            var service = new ArtifactService(Aderant.Build.Logging.NullLogger.Default);
            var metadata = service.GetBuildStateMetadata(BucketIds, DropLocation);

            WriteObject(metadata);
        }
    }
}
