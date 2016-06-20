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

        //public static string NugetServerUrl = "http://localhost:32789";
        public static string NugetServerUrl = "http://packages.ap.aderant.com/packages";

        public static string NugetServerClearCacheUrl = NugetServerUrl + "/nuget/ClearCache()";
    }
}