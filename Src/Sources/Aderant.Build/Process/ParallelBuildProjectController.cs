using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.MSBuild;
using Aderant.Build.Providers;

namespace Aderant.Build.Process {
    internal class ParallelBuildProjectController {

        public Project CreateProject(string branchDirectory, IModuleProvider moduleProvider, IEnumerable<string> modulesInBuild) {


            DependencyBuilder builder = new DependencyBuilder(moduleProvider);
            IEnumerable<Build> builds = builder.GetTree(true);

            List<Build> buildTree = new List<Build>();

            foreach (Build build in builds) {

                List<ExpertModule> modules = new List<ExpertModule>();

                foreach (ExpertModule module in build.Modules) {
                    foreach (var name in modulesInBuild) {
                        if (string.Equals(module.Name, name, StringComparison.OrdinalIgnoreCase)) {
                            modules.Add(module);
                        }
                    }
                }

                if (modules.Count > 0) {
                    Build item = new Build {
                        Modules = modules,
                        Order = build.Order
                    };
                    buildTree.Add(item);
                }
            }

            DynamicProject project = new DynamicProject(buildTree);
            return project.GenerateProject(branchDirectory);
        }

        public Project CreateProject(string branchDirectory) {
            DependencyBuilder builder = new DependencyBuilder(branchDirectory);
            IEnumerable<Build> builds = builder.GetTree(true);

            DynamicProject project = new DynamicProject(builds);
            Project generatedProject = project.GenerateProject(branchDirectory);

            return generatedProject;
        }

        public XElement CreateProjectDocument(Project project) {
            BuildElementVisitor visitor = new ParallelBuildVisitor();
            project.Accept(visitor);

            return visitor.GetDocument();
        }

        internal static void SaveBuildProject(string branchPath, XElement projectDocument) {
            string buildProject = Path.Combine(branchPath, "BranchBuild.proj");
            File.WriteAllText(buildProject, projectDocument.ToString(), Encoding.UTF8);
        }
    }
}