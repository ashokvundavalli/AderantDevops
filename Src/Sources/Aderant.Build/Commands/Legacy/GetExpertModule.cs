using System;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Commands {
    [Cmdlet("Get", "ExpertModule")]
    public class GetExpertModule : PSCmdlet {

        [Parameter(Mandatory = true,  Position = 0)]
        public string ModuleName { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public string BranchPath { get; set; }

        protected override void ProcessRecord() {
            string branchPath = ParameterHelper.GetBranchPath(BranchPath, this.SessionState);

            DependencyBuilder builder = new DependencyBuilder(branchPath);
            ExpertModule module = builder.GetAllModules().FirstOrDefault(x => string.Equals(x.Name, ModuleName, StringComparison.OrdinalIgnoreCase));
            
            if (module == null) {
                throw new PSArgumentOutOfRangeException("ModuleName");
            }

            WriteObject(module);
        }
    }
}
