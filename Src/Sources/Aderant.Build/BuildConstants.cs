namespace Aderant.Build {
    public static class BuildConstants {
        /// <summary>
        /// The build infrastructure directory
        /// </summary>
        public static string BuildInfrastructureDirectory = "Build.Infrastructure";

        /// <summary>
        /// The modules directory
        /// </summary>
        public static string ModulesDirectory = "Modules";

        public static string BranchNameVariable = "$(BranchName)";

        public static string NugetServerApiKey = "abc";
        
        public static string NugetServerUrl = "http://packages.ap.aderant.com/packages/nuget";

        public static string NugetServerClearCacheUrl = NugetServerUrl + "/ClearCache()";
    }
}