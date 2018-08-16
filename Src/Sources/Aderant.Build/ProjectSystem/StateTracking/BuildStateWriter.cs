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
        /// <param name="baseStateFile">The state file selected to build with.</param>
        /// <param name="bucket">The SHA of the directory for which the outputs are associated</param>
        /// <param name="currentOutputs">The outputs the build produced</param>
        /// <param name="artifacts">The artifacts this build produced</param>
        /// <param name="metadata">A description of the source tree</param>
        /// <param name="buildMetadata">A description of the current build environment</param>
        /// <param name="path">The path to write the file to</param>
        public string WriteStateFile(
            BuildStateFile baseStateFile,
            BucketId bucket,
            ProjectOutputCollection currentOutputs,
            IDictionary<string, ICollection<ArtifactManifest>> artifacts,
            SourceTreeMetadata metadata,
            BuildMetadata buildMetadata,
            string path) {

            string treeShaValue = null;
            if (metadata != null) {
                var treeSha = metadata.GetBucket(BucketId.Current);
                treeShaValue = treeSha.Id;
            }

            var stateFile = new BuildStateFile {
                TreeSha = treeShaValue,
                BucketId = bucket
            };

            if (baseStateFile != null) {
                stateFile.ParentId = baseStateFile.Id;
                stateFile.ParentBuildId = baseStateFile.BuildId;
                stateFile.ParentTreeSha = baseStateFile.TreeSha;
            }

            if (currentOutputs != null) {
                if (baseStateFile != null) {
                    MergeExistingOutputs(baseStateFile.BuildId, baseStateFile.Outputs, currentOutputs);
                }

                stateFile.Outputs = currentOutputs;
            }

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

            fileSystem.AddFile(path, stream => stateFile.Serialize(stream));

            return path;
        }

        private static void MergeExistingOutputs(string buildId, IDictionary<string, ProjectOutputs> oldOutput, IDictionary<string, ProjectOutputs> newOutput) {
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

                ProjectOutputCollection projectOutputCollection = null;
                var outputs = context.GetProjectOutputs();
                if (outputs != null) {
                    projectOutputCollection = outputs.GetProjectsForTag(tag);
                }

                ArtifactCollection artifactCollection = null;
                var artifacts = context.GetArtifacts();
                if (artifacts != null) {
                    artifactCollection = artifacts.GetArtifactsForTag(tag);
                }

                BuildStateFile file = context.GetStateFile(tag);
                var dropLocation = Path.Combine(context.GetDropLocation(tag), DefaultFileName);
                WriteStateFile(file, bucket, projectOutputCollection, artifactCollection, context.SourceTreeMetadata, context.BuildMetadata, dropLocation);
            }
        }
    }
}
