using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class GetBuildOutputs : BuildOperationContextTask {

        [Output]
        public string[] TestOutputDirectories { get; private set; }

        [Output]
        public string[] OutputDirectories { get; set; }

        [Output]
        public string[] SolutionRoots { get; private set; }

        public override bool ExecuteTask() {
            var projects = PipelineService.GetTrackedProjects();

            var trackedProjects =
                from trackedProject in projects
                join snapshot in PipelineService.GetAllProjectOutputs()
                    on trackedProject.ProjectGuid equals snapshot.ProjectGuid
                select new { TrackedProject = trackedProject, Snapshot = snapshot };

            TestOutputDirectories = trackedProjects
                .Where(s => s.Snapshot.IsTestProject)
                .Select(
                    s => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(s.TrackedProject.FullPath), s.Snapshot.OutputPath)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            OutputDirectories = trackedProjects
                .Where(s => !s.Snapshot.IsTestProject)
                .Select(
                    s => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(s.TrackedProject.FullPath), s.Snapshot.OutputPath)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            SolutionRoots = trackedProjects
                .Select(
                    s => s.TrackedProject.SolutionRoot)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return true;
        }
    }
}
