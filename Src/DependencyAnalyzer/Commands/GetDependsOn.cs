using System;
using System.Linq;
using System.Management.Automation;

namespace DependencyAnalyzer {
    [Cmdlet("Get", "ExpertModuleDependsOn")]
    public class GetDependsOn : PSCmdlet {

        [Parameter(Mandatory = false, Position = 0)]
        public string TargetModuleName { get; set; }

        [Parameter(Mandatory = false, ValueFromPipeline = true, Position = 1)]
        public ExpertModule TargetModule { get; set; }


        [Parameter(Mandatory = false, Position = 2)]
        public string BranchPath { get; set; }

        protected override void ProcessRecord() {
            string branchPath = ParameterHelper.GetBranchPath(BranchPath, this.SessionState);
            DependencyBuilder builder  = new DependencyBuilder(branchPath);

            if (TargetModuleName == null && TargetModule == null) {
                throw new ArgumentException("You must supply a SourceModule or SourceModuleName");
            }

            if (TargetModule == null) {
                TargetModule = builder.GetAllModules().Where(
                    x => x.Name.Equals(TargetModuleName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            }

            if (TargetModule == null) {
                throw new ArgumentException("TargetModuleName", string.Format("Could not find Module '{0}'", TargetModuleName));
            }

            WriteObject((
                from dependency in builder.GetModuleDependencies()
                where dependency.Provider.Equals(TargetModule)
                select dependency.Consumer).Distinct().ToArray(), true);
        }
    }
}