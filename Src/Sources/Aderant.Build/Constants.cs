using Aderant.Build.DependencyResolver.Resolvers;

namespace Aderant.Build {
    public static class Constants {
        static Constants() {
            NupkgResolver.Initialize();
        }

        /// <summary>
        /// The build infrastructure directory
        /// </summary>
        public static string BuildInfrastructureDirectory = "Build.Infrastructure";

        public static string PackageServerUrlV3 = "https://expertpackages.azurewebsites.net/v3/index.json";

        public static string PackageRepositoryUri = @"\\svfp311\PackageRepository\";

        public static string DatabasePackageUri = @"\\dfs.aderant.com\packages\ExpertDatabase";

        public static string OfficialNuGetUrlV3 = "https://api.nuget.org/v3/index.json";

        public static string OfficialNuGetUrl = "https://www.nuget.org/api/v2";

        private static string mainDependencyGroup;

        internal static string MainDependencyGroup {
            get {
                return mainDependencyGroup ?? (mainDependencyGroup = Paket.Constants.MainDependencyGroup.ToString());
            }
        }
    }

    public static class WellKnownPaths {
        /// <summary>
        /// Defines the file that marks a directory as a build contributor.
        /// </summary>
        public static string EntryPointFileName = "TFSBuild.proj";

        /// <summary>
        /// Defines the directory segment that marks a directory as a build contributor.
        /// </summary>
        public static string EntryPointFilePath = "Build\\" + EntryPointFileName;
    }
}
