using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.ProjectSystem;
using Aderant.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class GetBuildOutputs : BuildOperationContextTask {
        private List<OnDiskProjectInfo> projects;

        public string[] SolutionRoot { get; set; }

        public bool NormalizeOutputPath { get; set; }

        [Output]
        public string[] SolutionRoots { get; private set; }


        [Output]
        public ITaskItem[] TrackedProjects {
            get { return CreateProjectTaskItems(); }
        }

        public override bool ExecuteTask() {
            projects = PipelineService.GetTrackedProjects().ToList();

            if (SolutionRoot != null) {
                projects = projects.Where(s => {
                    foreach (var item in SolutionRoot) {
                        if (PathUtility.PathComparer.Equals(item, s.SolutionRoot)) {
                            return true;
                        }
                    }
                    return false;
                }).ToList();
            }

            SolutionRoots = projects
                .Select(s => s.SolutionRoot)
                .Distinct(PathUtility.PathComparer)
                .ToArray();

            return true;
        }

        private ITaskItem[] CreateProjectTaskItems() {
            List<ITaskItem> projectTaskItems = new List<ITaskItem>();
            HashSet<string> s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in projects) {
                var taskItem = new TaskItem(project.FullPath);

                string directoryOfFile = Path.GetDirectoryName(project.FullPath);
                string outputPath = Path.Combine(directoryOfFile, project.OutputPath);

                if (NormalizeOutputPath) {
                    outputPath = PathUtility.EnsureTrailingSlash(Path.GetFullPath(outputPath));
                }

                if (!s.TryGetValue(outputPath, out var actualOutputPath)) {
                    s.Add(outputPath);
                    actualOutputPath = outputPath;
                }

                taskItem.SetMetadata("OutputPath", actualOutputPath);
                taskItem.SetMetadata("SolutionRoot", project.SolutionRoot);

                projectTaskItems.Add(taskItem);
            }

            return projectTaskItems.ToArray();
        }
    }
}