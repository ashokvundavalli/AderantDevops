﻿using Aderant.Build.PipelineService;
using Aderant.Build.TeamFoundation;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class UpdateBuildRecordArtifacts : BuildOperationContextTask {

        public string DestinationPath { get; set; }

        public override bool ExecuteTask() {
            var commandBuilder = new VsoBuildCommandBuilder();

            var service = GetService<IVsoCommandService>();

            var builder = new ArtifactStagingPathBuilder(Context.ArtifactStagingDirectory, Context.BuildMetadata.BuildId, Context.SourceTreeMetadata);

            foreach (var artifact in service.GetAssociatedArtifacts()) {
                string fullPath = artifact.FullPath;

                if (!fullPath.StartsWith(@"\\")) {
                    artifact.ReplacePath(builder.StagingDirectory, DestinationPath);
                }

                if (!string.Equals(artifact.FullPath, fullPath)) {
                    Log.LogMessage(MessageImportance.High, $"Pending artifact location: {fullPath} --> {artifact.FullPath}");
                }

                var linkCommand = commandBuilder.LinkArtifact(artifact.Name, artifact.Type, artifact.ComputeVsoPath());

                Log.LogMessage(MessageImportance.High, linkCommand);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
