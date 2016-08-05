using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Runtime.ExceptionServices;
using Aderant.Build.Logging;

namespace Aderant.Build.Packaging {
    [Cmdlet("Package", "ExpertRelease")]
    public sealed class ExpertReleaseCommand : PSCmdlet {
        [Parameter(Mandatory = true, Position = 0)]
        public string ProductManifestPath { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public IEnumerable<string> Modules { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public IEnumerable<string> Folders { get; set; }

        [Parameter(Mandatory = true, Position = 3)]
        public string ProductDirectory { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            var assembler = new ProductAssembler(ProductManifestPath, new PowerShellLogger(Host));

            try {
                assembler.AssembleProduct(Modules, Folders, ProductDirectory);
            } catch (AggregateException ex) {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }
}