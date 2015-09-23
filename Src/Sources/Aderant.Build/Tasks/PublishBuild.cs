using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.Build.Client;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Creates a "fake" build in TFS. This is used as part of the build all to register each module as the build all progresses.
    /// This primary reason for this is so we can have the TFS retention policies manage the builds.
    /// </summary>
    public sealed class PublishBuild : Task {
        [Required]
        public string CurrentBuildUri { get; set; }

        [Required]
        public string DropLocation { get; set; }

        [Required]
        public string ModuleName { get; set; }

        [Required]
        public string BranchName { get; set; }

        [Required]
        public string FileVersion { get; set; }

        [Required]
        public string AssemblyVersion { get; set; }

        [Required]
        public string TeamFoundationServerUri { get; set; }

        [Required]
        public string TeamProject { get; set; }

        [Output]
        public TaskItem PublishedBuildUri { get; set; }

        public override bool Execute() {
            BuildDetailPublisher controller = new BuildDetailPublisher(TeamFoundationServerUri, TeamProject);
            IBuildDetail detail = controller.GetBuildDetails(CurrentBuildUri);

            ExpertBuildConfiguration configuration = new ExpertBuildConfiguration(BranchName) {
                ModuleName = ModuleName,
                DropLocation = DropLocation,
            };
            
            ExpertBuildDetail newBuildDetail = new ExpertBuildDetail(AssemblyVersion, FileVersion, configuration);
            newBuildDetail.BuildSummary = new BuildSummary();
            newBuildDetail.BuildSummary.Message = string.Format("Build created by Build All ({0})", detail.BuildNumber);

            IBuildDefinition definition = controller.CreateBuildDefinition(configuration);
            IBuildDetail newBuild = controller.CreateNewBuild(definition, newBuildDetail, detail.SourceGetVersion);

            AssociateBuild(detail, ModuleName, newBuild.Uri);

            // Return the published build Uri to the build workflow
            PublishedBuildUri = new TaskItem(newBuild.Uri.ToString());

            return !Log.HasLoggedErrors;
        }

        private void AssociateBuild(IBuildDetail currentBuild, string moduleName, Uri uri) {
            Log.LogMessage("Associating related builds {0} [{1}] ==> {2}", uri, moduleName, currentBuild.Uri);
            // Registered an associated child build with the current build.

            IBuildInformationNode informationNode = currentBuild.Information.CreateNode();
            informationNode.Type = "ExpertBuild";
            informationNode.Fields["ModuleName"] = moduleName;
            informationNode.Fields["RelatedBuildUri"] = uri.ToString();

            informationNode.Save();

            currentBuild.Information.Save();
        }
    }
}