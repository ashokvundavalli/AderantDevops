namespace Aderant.Build.Analyzer.Lists.QueryAll {
    /// <summary>
    /// The SQL Query All Code Analysis Rule references this class and iterates through
    /// the below members when determining the severity of a potential violation.
    /// 
    /// Types referenced below will automatically be flagged as 'safe' during rule evaluation.
    /// To add an additional type, simply add a new line to the relevant collection below, using the stated syntax.
    /// </summary>
    internal static class QueryAllWhitelists {
        // Syntax:
        // "Fully.Qualified.Type.Name"
        public static readonly string[] Types = {
            "Aderant.Query.ViewModels.BudgetingGLAccountMapping",
            "Aderant.Query.ViewModels.ConfigName"
        };
    }
}
