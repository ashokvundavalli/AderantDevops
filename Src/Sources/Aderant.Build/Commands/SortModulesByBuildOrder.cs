using System;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Commands {
    /// <summary>
    /// TODO: Restore
    /// </summary>
    /// <seealso cref="System.Management.Automation.PSCmdlet" />
    [Cmdlet("Sort", "ExpertModulesByBuildOrder")]
    public class SortModulesByBuildOrder : PSCmdlet {

        [Parameter(Mandatory = false, Position = 0)]
        public string[] ModuleNames { get; set; }

        [Parameter(Mandatory = false, ValueFromPipeline = true, Position = 1)]
        public ExpertModule[] Modules { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public string BranchPath { get; set; }

        [Parameter(Mandatory = true, Position = 3)]
        public string ProductManifestPath { get; set; }

        protected override void ProcessRecord() {
        }
    }
}