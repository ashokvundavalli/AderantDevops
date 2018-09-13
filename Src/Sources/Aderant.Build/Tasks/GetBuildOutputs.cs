using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    public class GetBuildOutputs : BuildOperationContextTask {
        private bool solutionRootFilter;

        public string SolutionRoot { get; set; }

        [Output]
        public string[] OutputDirectories { get; set; }

        [Output]
        public string[] TestOutputDirectories { get; private set; }

        [Output]
        public string[] SolutionRoots { get; private set; }

        public override bool ExecuteTask() {
            var projects = PipelineService.GetTrackedProjects();

            solutionRootFilter = !string.IsNullOrWhiteSpace(SolutionRoot);

            var trackedProjects =
                from trackedProject in projects
                join snapshot in PipelineService.GetAllProjectOutputs()
                    on trackedProject.ProjectGuid equals snapshot.ProjectGuid
                select new { TrackedProject = trackedProject, Snapshot = snapshot };

            TestOutputDirectories = trackedProjects
                .Where(s => SolutionRootFilter(s.TrackedProject.SolutionRoot) && s.Snapshot.IsTestProject)
                .Select(
                    s => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(s.TrackedProject.FullPath), s.Snapshot.OutputPath)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var outputDirectories = trackedProjects
                .Where(s => SolutionRootFilter(s.TrackedProject.SolutionRoot) && !s.Snapshot.IsTestProject)
                .Select(
                    s => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(s.TrackedProject.FullPath), s.Snapshot.OutputPath)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Prefer non-test outputs, return these items first
            OutputDirectories = outputDirectories.Concat(TestOutputDirectories).ToArray();

            SolutionRoots = trackedProjects
                .Select(
                    s => s.TrackedProject.SolutionRoot)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return true;
        }

        private bool SolutionRootFilter(string solutionRootFromProject) {
            if (!solutionRootFilter) {
                return true;
            }

            return string.Equals(SolutionRoot, solutionRootFromProject, StringComparison.OrdinalIgnoreCase);
        }
    }
}
