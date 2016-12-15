namespace Aderant.Build.Analyzer.Exclusions {
    /// <summary>
    /// This class is intentionally not static.
    /// By forcing this class to instantiate the exclusion list,
    /// it can then be garbage-collected after its single use has occurred,
    /// rather than remaining in the global memory pool indefinitely as a collection of static strings.
    /// </summary>
    internal class SqlInjectionExclusions {
        internal readonly string[] ExclusionsList = {
            "Services.Query\\Src\\",
            "AccountsPayable\\Src\\",
            "Services.Applications.Budgeting\\Src\\",
            "Services.Applications.Case\\Src\\",
            "Services.Applications.CheckRequest\\Src\\",
            "Services.Applications.Collections\\Src\\",
            "Services.Applications.Disbursement\\Src\\",
            "Services.Applications.EliteIntegration\\Src\\",
            "Services.Applications.EmployeeIntake\\Src\\",
            "Services.Applications.FileOpening\\Src\\",
            "Services.Applications.FirmControl\\Src\\",
            "Services.Applications.MatterPlanning\\Src\\",
            "Services.Applications.Prebill\\Src\\",
            "Services.Applications.Rates\\Src\\",
            "Time\\Src\\",
        };
    }
}
