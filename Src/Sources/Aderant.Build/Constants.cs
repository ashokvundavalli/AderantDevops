using System;
using System.IO;
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

        // Do not change this to a compile reference as there is a complex interplay between paket.exe and this assembly
        public static string PaketDependencies = "paket.dependencies";

        public static string PaketLock = "paket.lock";

        public static string PackageServerUrlV3 = "https://expertpackages.azurewebsites.net/v3/index.json";

        public static string PackageRepositoryUri = @"\\svfp311\PackageRepository\";

        public static string DatabasePackageUri = @"\\dfs.aderant.com\packages\ExpertDatabase";

        public static string OfficialNuGetUrlV3 = "https://api.nuget.org/v3/index.json";

        public static string OfficialNuGetUrl = "https://www.nuget.org/api/v2";

        internal static string MainDependencyGroup = "Main";

        internal const string LoggingArrow = "-> ";
    }

    public static class WellKnownPaths {
        /// <summary>
        /// Defines the common build directory name.
        /// </summary>
        public static readonly string BuildDirectory = "Build";

        /// <summary>
        /// Defines the file that marks a directory as a build contributor.
        /// </summary>
        public static readonly string EntryPointFileName = "TFSBuild.proj";

        /// <summary>
        /// Defines the directory segment that marks a directory as a build contributor.
        /// </summary>
        public static readonly string EntryPointFilePath = string.Concat(BuildDirectory, Path.DirectorySeparatorChar,  EntryPointFileName);
    }
}
