using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Extracts key properties from the context and returns them to MSBuild
    /// </summary>
    public sealed class GetPropertiesFromContext : BuildOperationContextTask {
        // TODO: This class also sets...what name to give it?
        public string ArtifactStagingDirectory { get; set; }

        [Output]
        public bool IsDesktopBuild {
            get { return Context.IsDesktopBuild; }
        }

        [Output]
        public string BuildSystemDirectory {
            get { return Context.BuildSystemDirectory; }
        }

        [Output]
        public string[] IncludePaths {
            get {
                return Context.Include;
            }
        }

        [Output]
        public ITaskItem[] ChangedFiles {
            get {
                return Context.SourceTreeMetadata.Changes.Select(x => (ITaskItem)new TaskItem(x.FullPath)).ToArray();
            }
        }

        [Output]
        public string BuildFlavor { get; set; }

        public override bool ExecuteTask() {
            base.Execute();

            var context = Context;

            SetFlavor(context);

            if (!string.IsNullOrWhiteSpace(ArtifactStagingDirectory)) {
                context.ArtifactStagingDirectory = ArtifactStagingDirectory;
            }

            if (context.BuildRoot != null) {
                Log.LogMessage(MessageImportance.Low, "Build root: " + context.BuildRoot);
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
