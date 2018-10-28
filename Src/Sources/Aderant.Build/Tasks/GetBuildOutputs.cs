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
        public string[] SolutionRoots { get; private set; }

        public override bool ExecuteTask() {
            var projects = PipelineService.GetTrackedProjects();

            solutionRootFilter = !string.IsNullOrWhiteSpace(SolutionRoot);

            var outputDirectories = projects
                .Where(s => SolutionRootFilter(s.SolutionRoot))
                .Select(
                    s => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(s.FullPath), s.OutputPath)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            OutputDirectories = outputDirectories.ToArray();

            SolutionRoots = projects
                .Select(s => s.SolutionRoot)
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
