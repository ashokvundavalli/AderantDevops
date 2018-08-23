using System;
using System.Runtime.Serialization;
using Aderant.Build.DependencyAnalyzer.Model;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem {
    /// <summary>
    /// Similar in concept to <see cref="ProjectBuildConfiguration" /> but represents the configuration of a solution to build.
    /// The main difference is the solution configuration usually has a space between "Any" and "CPU" where as the project
    /// configuration does not.
    /// </summary>
    [Serializable]
    [DataContract]
    [ProtoContract]
    public struct ConfigurationToBuild {

        public ConfigurationToBuild(string configurationToBuild) {
            var parts = configurationToBuild.Split('|');

            ConfigurationName = parts[0];
            PlatformName = parts[1];

            FullName = ComputeFullName(ConfigurationName, PlatformName);
        }

        public ConfigurationToBuild(string configurationName, string platformName) {
            this.ConfigurationName = configurationName;
            this.PlatformName = platformName;

            FullName = ComputeFullName(this.ConfigurationName, this.PlatformName);
        }

        public string ConfigurationName { get; }

        public string PlatformName { get; }

        public string FullName { get; }

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
