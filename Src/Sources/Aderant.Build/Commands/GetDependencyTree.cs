using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Commands {

    [Cmdlet("Get", "ExpertModuleDependencyTree")]
    public class GetDependencyTree : PSCmdlet {
        [Parameter(Mandatory = false, Position = 0)]
        public SwitchParameter RestrictToModulesInBranch {
            get;
            set;
        }

        [Parameter(Mandatory = false, Position = 1)]
        public string BranchPath {
            get;
            set;
        }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            string branchPath = ParameterHelper.GetBranchPath(BranchPath, this.SessionState);

            DependencyBuilder builder = new DependencyBuilder(branchPath);
            IEnumerable<Build> builds = builder.GetTree(RestrictToModulesInBranch.IsPresent);

            WriteToHost(builds);
        }

        private void WriteToHost(IEnumerable<Build> builds) {
            WriteObject("Builds");
            WriteObject(new string('=', 80));
            foreach (Build build in builds.OrderBy(b => b.Order)) {
                WriteObject("Build Level: " + build.Order);

                foreach (ExpertModule module in build.Modules) {
                    WriteObject("    " + module);
                }
            }
        }
    }
}