using System.Collections.Generic;
using Aderant.Build.Tasks;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    internal class ExtensibilityController {
        public static ExtensibilityImposition GetExtensibilityImposition(string modulesDirectory, string[] extensibilityFiles) {
            var alwaysBuildProjects = new List<string>();

            var result = new ExtensibilityImposition(alwaysBuildProjects);

            var globalProps = new Dictionary<string, string> { { "SolutionRoot", modulesDirectory } };

            using (var collection = new ProjectCollection(globalProps)) {
                collection.IsBuildEnabled = false;

                foreach (var file in extensibilityFiles) {
                    var loadProject = collection.LoadProject(file);
                    var projectItems = loadProject.GetItems("AlwaysBuildProjects");

                    foreach (var item in projectItems) {
                        alwaysBuildProjects.Add(item.EvaluatedInclude);
                    }
                }
            }

            return result;
        }
    }
}
