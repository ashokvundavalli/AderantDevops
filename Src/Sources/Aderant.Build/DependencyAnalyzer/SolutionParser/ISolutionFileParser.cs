namespace Aderant.Build.DependencyAnalyzer.SolutionParser {
    public interface ISolutionFileParser {
        ParseResult Parse(string solutionFile);
    }
}