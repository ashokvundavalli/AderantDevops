using System;
using System.IO;
using Aderant.Build.DependencyResolver;
using Aderant.Build.DependencyResolver.Resolvers;

namespace Aderant.Build {
    public static class Constants {
        static Constants() {
            PackageServerUrlV3 = WellKnownPackageSources.AzureHostedSources.Sources[0].Url;

            NupkgResolver.Initialize();
        }

        /// <summary>
        /// The build infrastructure directory
        /// </summary>
        public static readonly string BuildInfrastructureDirectory = "Build.Infrastructure";

        // Do not change this to reference paket.exe as there is a complex interplay between paket.exe and this assembly
        public static readonly string PaketDependencies = "paket.dependencies";

        public static readonly string PaketLock = "paket.lock";

        public static readonly string PackageServerUrlV3;

        public static readonly string OfficialNuGetUrlV3 = "https://api.nuget.org/v3/index.json";

        public static readonly string OfficialNuGetUrl = "https://www.nuget.org/api/v2";

        internal static readonly string MainDependencyGroup = "Main";

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

        internal static string ProfileHome { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aderant", "ContinuousDelivery");

    }
}
