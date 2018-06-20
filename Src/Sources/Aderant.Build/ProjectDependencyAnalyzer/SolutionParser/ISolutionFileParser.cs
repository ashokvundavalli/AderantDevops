namespace Aderant.Build.ProjectDependencyAnalyzer.SolutionParser {
    public interface ISolutionFileParser {
        ParseResult Parse(string solutionFile);
    }
}