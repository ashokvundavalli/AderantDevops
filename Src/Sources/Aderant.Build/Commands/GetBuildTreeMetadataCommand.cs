﻿using System.Management.Automation;
using Aderant.Build.Logging;
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
            System.Diagnostics.Debugger.Launch();

            var service = new ArtifactService(new PowerShellLogger(Host));
            var metadata = service.GetBuildStateMetadata(BucketIds, DropLocation);

            if (metadata.BuildStateFiles != null) {
                WriteInformation("Found " + metadata.BuildStateFiles.Count + " state files", null);
            }

            WriteObject(metadata);
        }
    }
}
