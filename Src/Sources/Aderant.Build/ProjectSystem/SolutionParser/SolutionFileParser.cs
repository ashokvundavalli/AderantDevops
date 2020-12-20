using System;
using System.Linq;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem.SolutionParser {
    internal class SolutionFileParser : ISolutionFileParser {
        public ParseResult Parse(string solutionFile) {
            SolutionFile file = SolutionFile.Parse(solutionFile);

            var result = new ParseResult {
                SolutionFile = solutionFile,
                ProjectsByGuid = file.ProjectsByGuid
                    .Where(s => IsProject(s.Value))
                    .ToDictionary(k => Guid.Parse(k.Key), v => new ProjectInSolutionWrapper(v.Value)),
                ProjectsInOrder = file.ProjectsInOrder.Where(s => IsProject(s)).Select(s => new ProjectInSolutionWrapper(s)),
            };

            return result;
        }

        private static bool IsProject(ProjectInSolution s) {
            return s.ProjectType != SolutionProjectType.SolutionFolder;
        }
    }
}
