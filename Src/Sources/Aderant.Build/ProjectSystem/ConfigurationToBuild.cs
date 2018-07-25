using System;
using Aderant.Build.DependencyAnalyzer.Model;

namespace Aderant.Build.ProjectSystem {
    /// <summary>
    /// Similar in concept to <see cref="ProjectBuildConfiguration"/> but represents the configuration of a solution to build.
    /// The main difference is the solution configuration usually has a space between "Any" and "CPU" where as the project configuration does not. 
    /// </summary>
    [Serializable]
    public struct ConfigurationToBuild {
        private readonly string configurationName;
        private readonly string platformName;
        private readonly string fullName;
        
        public ConfigurationToBuild(string configurationToBuild) {
            var parts = configurationToBuild.Split('|');

            configurationName = parts[0];
            platformName = parts[1];
            
            fullName = ComputeFullName(configurationName, platformName);
        }

        public ConfigurationToBuild(string configurationName, string platformName) {
            this.configurationName = configurationName;
            this.platformName = platformName;

            fullName = ComputeFullName(this.configurationName, this.platformName);
        }


        public string ConfigurationName {
            get {
                return configurationName;
            }
        }

        public string PlatformName {
            get {
                return platformName;
            }
        }

        public string FullName {
            get {
                return fullName;
            }
        }

        internal static string ComputeFullName(string configurationName, string platformName) {
            if (!string.IsNullOrEmpty(platformName)) {
                return string.Concat(configurationName, "|", platformName);
            }
            return configurationName;
        }
   

        public static string Debug = "Debug";

        public static string Release = "Release";

        public static string AnyCPU = "Any CPU";

        public static ConfigurationToBuild Default = Create(Debug, AnyCPU);

        private static ConfigurationToBuild Create(string configuration, string platform) {
            return new ConfigurationToBuild(configuration, platform);
        }
    }
}
