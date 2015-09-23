using Microsoft.TeamFoundation.Build.Client;

namespace Aderant.Build {
    internal interface IBuildProcessTemplate {
        /// <summary>
        /// Configures the definition to use the build process.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="server">The server.</param>
        /// <param name="buildDefinition">The build definition.</param>
        void ConfigureDefinition(ExpertBuildConfiguration configuration, IBuildServer server, IBuildDefinition buildDefinition);

        void AddProjectNodes(IBuildDetail buildDetail);
    }
}