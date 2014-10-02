using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Xml.Linq;
using Aderant.Build.BuildProcess;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.MSBuild;

namespace Aderant.Build.Commands {

    [Cmdlet(VerbsCommon.New, "ExpertBranchBuildProject")]
    public class NewParallelBuildProject : PSCmdlet {

        public string BranchModulePath { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            string branchPath = ParameterHelper.GetBranchModulesDirectory(BranchModulePath, SessionState);

            DependencyBuilder builder = new DependencyBuilder(branchPath);
            IEnumerable<Build> builds = builder.GetTree(true);

            DynamicProject project = new DynamicProject(builds);
            Project generatedProject = project.GenerateProject(branchPath);

            SaveBuildProject(branchPath, generatedProject);
        }

        private void SaveBuildProject(string branchPath, Project project) {
            BuildElementVisitor visitor = new ParallelBuildVisitor();
            project.Accept(visitor);

            XElement projectDocument = visitor.GetDocument();

            string buildProject = Path.Combine(branchPath, "BranchBuild.proj");

            File.WriteAllText(buildProject, projectDocument.ToString());
        }
    }
}