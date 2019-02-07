namespace Aderant.Build {
    public static class Constants {
        /// <summary>
        /// The build infrastructure directory
        /// </summary>
        public static string BuildInfrastructureDirectory = "Build.Infrastructure";

        /// <summary>
        /// The modules directory
        /// </summary>
        public static string ModulesDirectory = "Modules";

        public static string NugetServerApiKey = "abc";

        public static string PackageServerUrl = "http://packages.ap.aderant.com/packages/nuget";

        public static string PackageRepositoryUri = @"\\svfp311\PackageRepository\";

        public static string DatabasePackageUri = @"\\dfs.aderant.com\packages\ExpertDatabase";

        public static string NugetServerClearCacheUrl = PackageServerUrl + "/ClearCache()";

        public static string DefaultNuGetServer = "https://www.nuget.org/api/v2";

        internal static string MainDependencyGroup = Paket.Constants.MainDependencyGroup.ToString();
    }

    public static class WellKnownPaths {
        /// <summary>
        /// Defines the file that marks a directory as a build contributor.
        /// </summary>
        public static string EntryPointFileName = "TFSBuild.proj";

        /// <summary>
        /// Defines the directory prefix and file that marks a directory as a build contributor.
        /// </summary>
        public static string EntryPointFilePath = "Build\\" + EntryPointFileName;
    }
}
