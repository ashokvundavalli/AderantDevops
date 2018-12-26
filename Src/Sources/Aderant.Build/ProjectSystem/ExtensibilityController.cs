using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    internal class ExtensibilityController {
        public static ExtensibilityImposition GetExtensibilityImposition(string modulesDirectory, string[] extensibilityFiles) {
            var alwaysBuildProjects = new List<string>();
            var projectMetadataLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var globalProps = new Dictionary<string, string> { { "SolutionRoot", modulesDirectory } };

            using (var collection = new ProjectCollection(globalProps)) {
                collection.IsBuildEnabled = false;

                foreach (var file in extensibilityFiles) {
                    var loadProject = collection.LoadProject(file);

                    AddAlwaysBuild(loadProject, alwaysBuildProjects);

                    ExtractAssemblyAliases(loadProject, projectMetadataLookup);
                }
            }

            return new ExtensibilityImposition(alwaysBuildProjects) {
                AliasMap = projectMetadataLookup
            };
        }

        private static void ExtractAssemblyAliases(Project loadProject, Dictionary<string, string> projectMetadataLookup) {
            var projectItems = loadProject.GetItems("AssemblyAlias");

            if (projectItems != null) {
                foreach (ProjectItem projectItem in projectItems) {
                    foreach (ProjectMetadata metadata in projectItem.DirectMetadata) {
                        if (string.Equals(metadata.Name, "Name", StringComparison.OrdinalIgnoreCase)) {
                            projectMetadataLookup[metadata.EvaluatedValue] = projectItem.EvaluatedInclude;
                            break;
                        }
                    }
                }
            }
        }

        private static void AddAlwaysBuild(Project loadProject, List<string> alwaysBuildProjects) {
            var projectItems = loadProject.GetItems("AlwaysBuildProjects");

            foreach (var item in projectItems) {
                alwaysBuildProjects.Add(item.EvaluatedInclude);
            }
        }
    }
}
