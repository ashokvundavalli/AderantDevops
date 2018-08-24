using Aderant.Build.PipelineService;
using Aderant.Build.TeamFoundation;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class ExecutePendingVsoCommands : BuildOperationContextTask {

        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }

        public override bool ExecuteTask() {
             var commandBuilder = new VsoBuildCommandBuilder();

            var service = GetService<IVsoCommandService>();

            foreach (var artifact in service.GetAssociatedArtifacts()) {
                string fullPath = artifact.FullPath;

                artifact.ReplacePath(SourcePath, DestinationPath);

                if (!string.Equals(artifact.FullPath, fullPath)) {
                    Log.LogMessage(MessageImportance.High, $"Updated artifact location: {fullPath} --> {artifact.FullPath}");
                }

                var linkCommand = commandBuilder.LinkArtifact(artifact.Name, artifact.Type, artifact.ComputeVsoPath());

                Log.LogMessage(MessageImportance.High, linkCommand);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
