using System;
using System.Globalization;

namespace Aderant.Build {
    internal class ExpertBuildDetail {
        private readonly ExpertBuildConfiguration configuration;
        private readonly string buildNumber;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertBuildDetail"/> class.
        /// </summary>
        /// <param name="assemblyVersion">The assembly version.</param>
        /// <param name="fileVersion">The file version.</param>
        /// <param name="configuration">The configuration.</param>
        public ExpertBuildDetail(string assemblyVersion, string fileVersion, ExpertBuildConfiguration configuration) {
            this.configuration = configuration;
            if (string.IsNullOrEmpty(configuration.DropLocation)) {
                throw new ArgumentNullException("No DropLocation specified");
            }

            this.buildNumber = string.Format(CultureInfo.InvariantCulture, "{0} ({1})", fileVersion, configuration.ModuleName); // Matches the BuildNumber format from ModuleBuild.proj - not that it has to but consistency is nice.

            string dropLocationForModule = configuration.BuildDropLocationForModule();

            DropLocation = string.Join("\\", dropLocationForModule, assemblyVersion, fileVersion);
        }

        /// <summary>
        /// Gets the build number. This is the FileVersion of the build.
        /// </summary>
        /// <value>
        /// The build number.
        /// </value>
        public string BuildNumber {
            get { return buildNumber; }
        }

        /// <summary>
        /// Gets the name of the module.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public string ModuleName {
            get {
                return configuration.ModuleName;
            }
        }

        /// <summary>
        /// Gets the calculated drop location for the build instance.
        /// </summary>
        /// <value>
        /// The drop location.
        /// </value>
        public string DropLocation { get; private set; }

        public string LogLocation { get; set; }

        public BuildSummary BuildSummary { get; set; }
    }
}