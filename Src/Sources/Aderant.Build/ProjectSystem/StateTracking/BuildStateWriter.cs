using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Aderant.Build.VersionControl;

namespace Aderant.Build.ProjectSystem.StateTracking {
    internal class BuildStateWriter {
        private readonly IFileSystem fileSystem;

        public BuildStateWriter()
            : this(new PhysicalFileSystem()) {
        }

        internal BuildStateWriter(IFileSystem fileSystem) {
            this.fileSystem = fileSystem;
        }

        public static string DefaultFileName { get; private set; } = "buildstate.metadata";

        /// <summary>
        /// Serializes the build state file
        /// </summary>
        /// <param name="previousBuild">The state file selected to build with.</param>
        /// <param name="bucket">The SHA of the directory for which the outputs are associated</param>
        /// <param name="currentOutputs">The outputs the build produced</param>
        /// <param name="artifacts">The artifacts this build produced</param>
        /// <param name="metadata">A description of the source tree</param>
        /// <param name="buildMetadata">A description of the current build environment</param>
        /// <param name="destinationPath">The path to write the file to</param>
        public string WriteStateFile(
            BuildStateFile previousBuild,
            BucketId bucket,
            ProjectOutputSnapshot currentOutputs,
            IDictionary<string, ICollection<ArtifactManifest>> artifacts,
            SourceTreeMetadata metadata,
            BuildMetadata buildMetadata,
            string destinationPath) {

            string treeShaValue = null;
            if (metadata != null) {
                var treeSha = metadata.GetBucket(BucketId.Current);
                treeShaValue = treeSha.Id;
            }

            var stateFile = new BuildStateFile {
                TreeSha = treeShaValue,
                BucketId = bucket
            };

            if (previousBuild != null) {
                stateFile.ParentId = previousBuild.Id;
                stateFile.ParentBuildId = previousBuild.BuildId;
                stateFile.ParentTreeSha = previousBuild.TreeSha;
            }

            if (currentOutputs == null) {
                currentOutputs = new ProjectOutputSnapshot();
            }

            if (previousBuild != null) {
                MergeExistingOutputs(previousBuild.BuildId, previousBuild.Outputs, currentOutputs);
            }

            stateFile.Outputs = currentOutputs;

            if (artifacts != null) {
                stateFile.Artifacts = artifacts;
            }

            if (buildMetadata != null) {
                stateFile.BuildId = buildMetadata.BuildId.ToString(CultureInfo.InvariantCulture);

                if (buildMetadata.IsPullRequest) {
                    stateFile.PullRequestInfo = buildMetadata.PullRequest;
                } else {
                    stateFile.ScmBranch = buildMetadata.ScmBranch;
                    stateFile.ScmCommitId = buildMetadata.ScmCommitId;
                }
            }

            stateFile.DropLocation = null;

            fileSystem.AddFile(destinationPath, stream => stateFile.Serialize(stream));

            return destinationPath;
        }

        private static void MergeExistingOutputs(string buildId, IDictionary<string, OutputFilesSnapshot> oldOutput, IDictionary<string, OutputFilesSnapshot> newOutput) {
            foreach (var projectOutputs in oldOutput) {
                if (!newOutput.ContainsKey(projectOutputs.Key)) {
                    var outputs = projectOutputs.Value;
                    outputs.Origin = buildId;

                    newOutput[projectOutputs.Key] = outputs;
                }
            }
        }

        public void WriteStateFiles(BuildOperationContext context) {
            IReadOnlyCollection<BucketId> buckets = context.SourceTreeMetadata.GetBuckets();

            foreach (var bucket in buckets) {
                var tag = bucket.Tag;

                ProjectOutputSnapshot projectOutputSnapshot = null;
                var outputs = context.GetProjectOutputs();
                if (outputs != null) {
                    projectOutputSnapshot = outputs.GetProjectsForTag(tag);
                }

                ArtifactCollection artifactCollection = null;
                var artifacts = context.GetArtifacts();
                if (artifacts != null) {
                    artifactCollection = artifacts.GetArtifactsForTag(tag);
                }

                BuildStateFile previousBuild = context.GetStateFile(tag);

                WriteStateFile(previousBuild, bucket, projectOutputSnapshot, artifactCollection, context);
            }
        }

        private void WriteStateFile(BuildStateFile previousBuild, BucketId bucket, ProjectOutputSnapshot projectOutputSnapshot, ArtifactCollection artifactCollection, BuildOperationContext context) {
            var pathBuilder = new ArtifactStagingPathBuilder(context);
            var file = pathBuilder.BuildPath(bucket.Tag);
            file = Path.Combine(file, DefaultFileName);

            string stateFile = WriteStateFile(previousBuild, bucket, projectOutputSnapshot, artifactCollection, context.SourceTreeMetadata, context.BuildMetadata, file);

            context.WrittenStateFiles.Add(stateFile);
        }
    }
}
