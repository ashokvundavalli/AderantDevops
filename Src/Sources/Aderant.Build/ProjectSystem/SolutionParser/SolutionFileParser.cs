using System;
using System.Linq;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem.SolutionParser {
    internal class SolutionFileParser : ISolutionFileParser {
        public ParseResult Parse(string solutionFile) {
            SolutionFile file = SolutionFile.Parse(solutionFile);

            var result = new ParseResult {
                SolutionFile = solutionFile,
                ProjectsByGuid = file.ProjectsByGuid.ToDictionary(k => Guid.Parse(k.Key), v => v.Value),
                ProjectsInOrder = file.ProjectsInOrder,
                SolutionConfigurations = file.SolutionConfigurations
            };

            return result;
        }
    }
}
