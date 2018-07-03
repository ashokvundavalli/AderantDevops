namespace Aderant.Build.ProjectSystem.SolutionParser {
    internal interface ISolutionFileParser {
        ParseResult Parse(string solutionFile);
    }
}
