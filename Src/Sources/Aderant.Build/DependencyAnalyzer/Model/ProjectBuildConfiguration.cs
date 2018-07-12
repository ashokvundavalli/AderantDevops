namespace Aderant.Build.DependencyAnalyzer.Model {

    /// <summary>
    /// The build configuration assigned to a project within a solution.
    /// </summary>
    internal class ProjectBuildConfiguration {

        public static ProjectBuildConfiguration DebugOnAnyCpu = new ProjectBuildConfiguration("Debug", "AnyCPU");

        public static ProjectBuildConfiguration ReleaseOnAnyCpu = new ProjectBuildConfiguration("Release", "AnyCPU");

        public ProjectBuildConfiguration(string configurationName, string platformName) {
            ConfigurationName = configurationName;
            PlatformName = platformName;
        }

        public string ConfigurationName { get; }
        public string PlatformName { get; }

        public static ProjectBuildConfiguration GetConfiguration(string configurationName, string platformName) {
            if (configurationName == DebugOnAnyCpu.ConfigurationName) {
                if (platformName == DebugOnAnyCpu.PlatformName) {
                    return DebugOnAnyCpu;
                }
            }

            if (configurationName == ReleaseOnAnyCpu.ConfigurationName) {
                if (platformName == ReleaseOnAnyCpu.PlatformName) {
                    return ReleaseOnAnyCpu;
                }
            }

            return null;
        }
    }
}
