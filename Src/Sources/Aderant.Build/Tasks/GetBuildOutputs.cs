using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class GetBuildOutputs : BuildOperationContextTask {

        [Output]
        public string[] TestAssemblyDirectories { get; private set; }

        public override bool ExecuteTask() {
            var query =
                from tp in PipelineService.GetTrackedProjects()
                join snapshot in PipelineService.GetAllProjectOutputs()
                    on tp.ProjectGuid equals snapshot.ProjectGuid
                where snapshot.IsTestProject
                select new { TrackedProject = tp, Snapshot = snapshot };

            TestAssemblyDirectories = query
                .Select(
                    s => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(s.TrackedProject.FullPath), s.Snapshot.OutputPath)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return true;
        }
    }
}
