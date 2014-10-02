using System.IO;
using System.Management.Automation;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using DependencyAnalyzer;

namespace Aderant.Build.Commands {
    [Cmdlet("Get", "ExpertModuleDependencyGraph")]
    public class GetDependencyGraph : PSCmdlet {
        
        [Parameter(Mandatory = true, Position = 0)]
        public string OutputPath { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public SwitchParameter RestrictToModulesInBranch { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public SwitchParameter IncludeBuilds { get; set; }

        [Parameter(Mandatory = false, Position = 3)]
        public string BranchPath { get; set; }

        protected override void ProcessRecord() {
            string branchPath = ParameterHelper.GetBranchPath(BranchPath, this.SessionState);

            if(!OutputPath.EndsWith(".dgml")) {
                throw new PSArgumentException("OutputPath must be a .dgml file");
            }

            if (!Directory.Exists(Path.GetDirectoryName(OutputPath))) {
                throw new PSArgumentException(string.Format("The path '{0}' does not exist", Path.GetDirectoryName(OutputPath)));
            }

            DependencyBuilder builder = new DependencyBuilder(branchPath);
            builder.BuildDgmlDocument(IncludeBuilds, RestrictToModulesInBranch).Save(OutputPath, SaveOptions.None);
            WriteObject(string.Format("DGML File written to {0}", OutputPath));
        }
    }
}