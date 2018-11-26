using System;
using System.Collections.Generic;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;

namespace Aderant.Build.Packaging {

    [Cmdlet("Package", "ExpertRelease")]
    [OutputType(typeof(IProductAssemblyResult))]
    public sealed class PackageExpertReleaseCommand : PSCmdlet {
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string ProductManifestXml { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public PSObject[] Modules { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public IEnumerable<string> Folders { get; set; }

        [Parameter(Mandatory = true, Position = 3)]
        [ValidateNotNullOrEmpty]
        public string ProductDirectory { get; set; }

        [Parameter(Mandatory = false, Position = 4)]
        public string TfvcSourceGetVersion { get; set; }

        [Parameter(Mandatory = false, Position = 5)]
        public string TeamProject { get; set; }

        [Parameter(Mandatory = false, Position = 6)]
        public string TfvcBranch { get; set; }

        [Parameter(Mandatory = false, Position = 7)]
        public string TfsBuildId { get; set; }

        [Parameter(Mandatory = false, Position = 8)]
        public string TfsBuildNumber { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            var itemMap = new List<ExpertModule>();

            for (var i = 0; i < Modules.Length; i++) {
                PSObject manifestEntry = Modules[i];
                string dependencyGroup = "";
                string dependencyName = null;

                foreach (PSPropertyInfo moduleProperty in manifestEntry.Properties) {
                    if (string.Equals(moduleProperty.Name, nameof(ExpertModule.Name), StringComparison.OrdinalIgnoreCase)) {
                        dependencyName = (string)moduleProperty.Value;
                    }

                    if (string.Equals(moduleProperty.Name, nameof(ExpertModule.DependencyGroup), StringComparison.OrdinalIgnoreCase)) {
                        dependencyGroup = (string)moduleProperty.Value;
                    }
                }

                if (dependencyName != null) {
                    itemMap.Add(new ExpertModule(dependencyName) {
                        DependencyGroup = dependencyGroup
                    });
                }
            }

            try {
                var assembler = new ProductAssembler(ProductManifestXml, new PowerShellLogger(Host));
                var result = assembler.AssembleProduct(itemMap, Folders, ProductDirectory, TfvcSourceGetVersion, TeamProject, TfvcBranch, TfsBuildId, TfsBuildNumber);

                WriteObject(result);
            } catch (AggregateException ex) {
                throw ex.Flatten().InnerException;
            }
        }
    }
}