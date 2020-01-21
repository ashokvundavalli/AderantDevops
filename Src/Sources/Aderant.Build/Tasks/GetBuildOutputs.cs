﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    public class GetBuildOutputs : BuildOperationContextTask {
        private IEnumerable<OnDiskProjectInfo> projects;
        private bool solutionRootFilter;

        public string SolutionRoot { get; set; }

        [Output]
        public string[] SolutionRoots { get; private set; }

        [Output]
        public ITaskItem[] TrackedProjects {
            get { return CreateProjectTaskItems(projects); }
        }

        public override bool ExecuteTask() {
            projects = PipelineService.GetTrackedProjects();

            solutionRootFilter = !string.IsNullOrWhiteSpace(SolutionRoot);

            SolutionRoots = projects
                .Select(s => s.SolutionRoot)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return true;
        }

        private ITaskItem[] CreateProjectTaskItems(IEnumerable<OnDiskProjectInfo> projects) {
            List<ITaskItem> projectTaskItems = new List<ITaskItem>();

            foreach (var project in projects) {
                var taskItem = new TaskItem(project.FullPath);

                string directoryOfFile = Path.GetDirectoryName(project.FullPath);
                string outputPath = Path.Combine(directoryOfFile, project.OutputPath);

                taskItem.SetMetadata("OutputPath", Path.GetFullPath(outputPath));
                taskItem.SetMetadata("SolutionRoot", project.SolutionRoot);

                projectTaskItems.Add(taskItem);
            }

            return projectTaskItems.ToArray();
        }

        private bool SolutionRootFilter(string solutionRootFromProject) {
            if (!solutionRootFilter) {
                return true;
            }

            return string.Equals(SolutionRoot, solutionRootFromProject, StringComparison.OrdinalIgnoreCase);
        }
    }
}