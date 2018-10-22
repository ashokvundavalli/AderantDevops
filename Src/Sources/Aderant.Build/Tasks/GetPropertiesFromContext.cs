using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Extracts key properties from the context and returns them to MSBuild
    /// </summary>
    public sealed class GetPropertiesFromContext : BuildOperationContextTask {
        // TODO: This class also sets...what name to give it?
        public string ArtifactStagingDirectory { get; set; }

        [Output]
        public bool IsDesktopBuild { get; set; }

        [Output]
        public string BuildSystemDirectory { get; set; }

        [Output]
        public string BuildFlavor { get; set; }

        [Output]
        public ITaskItem[] PropertiesToCreate { get; set; }

        public override bool ExecuteTask() {
            base.Execute();

            var context = Context;

            IsDesktopBuild = context.IsDesktopBuild;
            BuildSystemDirectory = context.BuildSystemDirectory;

            SetFlavor(context);

            if (!string.IsNullOrWhiteSpace(ArtifactStagingDirectory)) {
                context.ArtifactStagingDirectory = ArtifactStagingDirectory;
            }

            PipelineService.Publish(context);

            return !Log.HasLoggedErrors;
        }

        private void SetFlavor(BuildOperationContext context) {
            if (context.BuildMetadata != null) {
                if (!string.IsNullOrEmpty(context.BuildMetadata.Flavor)) {
                    BuildFlavor = context.BuildMetadata.Flavor;
                } else {
                    if (Context.Switches.Release) {
                        BuildFlavor = "Release";
                    } else {
                        BuildFlavor = "Debug";
                    }

                    context.BuildMetadata.Flavor = BuildFlavor;
                }
            }
        }
    }
}
