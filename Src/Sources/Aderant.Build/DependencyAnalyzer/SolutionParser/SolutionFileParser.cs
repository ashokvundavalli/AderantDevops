using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectDependencyAnalyzer.SolutionParser {
    public class SolutionFileParser : ISolutionFileParser {
        public ParseResult Parse(string solutionFile) {
            SolutionFile file = SolutionFile.Parse(solutionFile);

            return new ParseResult {
                SolutionFile = solutionFile,
                ProjectsByGuid = file.ProjectsByGuid,
                ProjectsInOrder = file.ProjectsInOrder,
                SolutionConfigurations = file.SolutionConfigurations
            };
        }
    }
}