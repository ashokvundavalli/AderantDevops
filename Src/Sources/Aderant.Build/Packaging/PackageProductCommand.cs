using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Runtime.ExceptionServices;
using Aderant.Build.Logging;
using Microsoft.Build.Framework;
using Microsoft.TeamFoundation.Build.Client;

namespace Aderant.Build.Packaging {
    [Cmdlet("Package", "ExpertRelease")]
    [OutputType(typeof(IProductAssemblyResult))]
    public sealed class ExpertReleaseCommand : PSCmdlet {
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string ProductManifestPath { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public IEnumerable<string> Modules { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public IEnumerable<string> Folders { get; set; }

        [Parameter(Mandatory = true, Position = 3)]
        [ValidateNotNullOrEmpty]
        public string ProductDirectory { get; set; }

        [Parameter(Mandatory = false, Position = 4)]
        public string TfvcSourceGetVersion { get; set; }

        [Parameter(Mandatory = false, Position = 5)]
        public string TeamProject {
            get; set;
        }

        [Parameter(Mandatory = false, Position = 6)]
        public string TfvcBranch {
            get; set;
        }

        [Parameter(Mandatory = false, Position = 7)]
        public string TfsBuildId {
            get; set;
        }

        [Parameter(Mandatory = false, Position = 8)]
        public string TfsBuildNumber {
            get; set;
        }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            try {
                var assembler = new ProductAssembler(ProductManifestPath, new PowerShellLogger(Host));
                var result = assembler.AssembleProduct(Modules, Folders, ProductDirectory, TfvcSourceGetVersion, TeamProject, TfvcBranch, TfsBuildId, TfsBuildNumber);

                WriteObject(result);
            } catch (AggregateException ex) {
                throw ex.Flatten().InnerException;
            }
        }
    }
}