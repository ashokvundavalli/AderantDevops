using System.IO;
using System.Management.Automation;
using System.Text;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.MSBuild;
using Aderant.Build.Process;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.New, "ExpertBranchBuildProject")]
    public class NewParallelBuildProject : PSCmdlet {
        public string BranchModulePath { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            string branchPath = ParameterHelper.GetBranchModulesDirectory(BranchModulePath, SessionState);

            ParallelBuildProjectController controller = new ParallelBuildProjectController();
            Project project = controller.CreateProject(branchPath);
            XElement projectDocument = controller.CreateProjectDocument(project);

            ParallelBuildProjectController.SaveBuildProject(branchPath, projectDocument);
        }
    }
}