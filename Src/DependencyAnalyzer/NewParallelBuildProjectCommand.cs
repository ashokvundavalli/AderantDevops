using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Xml.Linq;
using DependencyAnalyzer.MSBuild;
using DependencyAnalyzer.Process;
using Project = DependencyAnalyzer.MSBuild.Project;

namespace DependencyAnalyzer {
    [Cmdlet(VerbsCommon.New, "ParallelBuildProject")]
    public class NewParallelBuildProjectCommand : PSCmdlet {
        [Parameter(Mandatory = false, Position = 1)]
        public string BranchModulePath {
            get;
            set;
        }

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
            BuildElementVisitor visitor = new BuildElementVisitor();
            project.Accept(visitor);

            XElement projectDocument = visitor.GetDocument();

            XElement afterCompileElement = projectDocument.Elements().FirstOrDefault(elm => elm.FirstAttribute != null && elm.FirstAttribute.Value == "AfterCompile");
            if (afterCompileElement != null) {
                afterCompileElement.AddBeforeSelf(new XComment("Do not use CallTarget here - only DependsOnTargets target. It is possible to trigger an MS Build bug due to the call graph complexity: \"MSB0001:Internal MSBuild Error: We should have already left any legacy call target scopes\" "));
                afterCompileElement.AddBeforeSelf(new XComment("This target defines the end point of the project, each build level will be called from 0 .. n before this executes"));
            }

            string buildProject = Path.Combine(branchPath, "M.proj");

            File.WriteAllText(buildProject, projectDocument.ToString());
        }
    }
}