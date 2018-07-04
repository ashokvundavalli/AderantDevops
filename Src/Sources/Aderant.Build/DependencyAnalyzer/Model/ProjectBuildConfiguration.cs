namespace Aderant.Build.DependencyAnalyzer.Model {

    /// <summary>
    /// The build configuration assigned to a project within a solution.
    /// </summary>
    internal class ProjectBuildConfiguration {

        public ProjectBuildConfiguration(string configurationName, string platformName) {
            ConfigurationName = configurationName;
            PlatformName = platformName;
        }

        public string ConfigurationName { get; }
        public string PlatformName { get; }

        /// <summary>
        /// The release flavor
        /// </summary>
        public static string ReleaseAnyCpu = "Release|Any CPU";

        /// <summary>
        /// The debug flavor
        /// </summary>
        public static string DebugAnyCpu = "Debug|Any CPU";
    }
}
