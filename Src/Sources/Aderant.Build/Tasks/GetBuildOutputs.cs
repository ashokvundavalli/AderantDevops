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

        public override bool ExecuteTask() {
            var trackedProjects =
                from tp in PipelineService.GetTrackedProjects()
                join snapshot in PipelineService.GetAllProjectOutputs()
                    on tp.ProjectGuid equals snapshot.ProjectGuid
                select new { TrackedProject = tp, Snapshot = snapshot };

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

            return true;
        }
    }
}
