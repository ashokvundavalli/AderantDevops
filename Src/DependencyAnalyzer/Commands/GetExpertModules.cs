using System.Linq;
using System.Management.Automation;

namespace DependencyAnalyzer {
    [Cmdlet("Get", "ExpertModules")]
    public class GetExpertModules : PSCmdlet {

        [Parameter(Mandatory = false, Position = 0)]
        public object[] ModuleNames { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public string BranchPath { get; set; }

        protected override void ProcessRecord() {
            string branchPath = ParameterHelper.GetBranchPath(BranchPath, this.SessionState);
            DependencyBuilder builder = new DependencyBuilder(branchPath);
            ExpertModule[] modules = builder.GetAllModules().ToArray();
            

            if (ModuleNames != null && ModuleNames.Length > 0) {
                string[] moduleNamesArray = ModuleNames.Select(x => x.ToString()).ToArray();
                modules = modules.Where(x => moduleNamesArray.Contains(x.Name)).ToArray();
            }
            
            WriteObject(modules, true);
        }
    }
}