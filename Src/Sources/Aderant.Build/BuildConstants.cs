using Paket;

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
        
        public static string PackageServerUrl = "http://packages.ap.aderant.com/packages/nuget";

        public static string NugetServerClearCacheUrl = PackageServerUrl + "/ClearCache()";

        public static string DefaultNuGetServer = "https://www.nuget.org/api/v2";

        internal static string MainDependencyGroup = Constants.MainDependencyGroup.ToString();
    }
}