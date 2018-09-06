using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.PipelineService;
using Aderant.Build.TeamFoundation;
using Aderant.Build.VersionControl;

namespace Aderant.Build.ProjectSystem.StateTracking {
    internal class BuildStateWriter {
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;

        public BuildStateWriter(ILogger logger)
            : this(new PhysicalFileSystem(), logger) {
        }

        internal BuildStateWriter(IFileSystem fileSystem, ILogger logger) {
            this.fileSystem = fileSystem;
            this.logger = logger;
            this.WrittenStateFiles = new List<string>();
        }

        public static string DefaultFileName { get; private set; } = "buildstate.metadata";

        public ICollection<string> WrittenStateFiles { get; set; }

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
            IEnumerable<OutputFilesSnapshot> currentOutputs,
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
                currentOutputs = Enumerable.Empty<OutputFilesSnapshot>();
            }

            if (previousBuild != null) {
                MergeExistingOutputs(previousBuild.BuildId, previousBuild.Outputs, currentOutputs);
            }

            stateFile.Outputs = currentOutputs.ToDictionary(key => key.ProjectFile, value => value);

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

            stateFile.PrepareForSerialization();

            logger.Info("Writing state file to: " + destinationPath);
            fileSystem.AddFile(destinationPath, stream => stateFile.Serialize(stream));

            return destinationPath;
        }

        private static void MergeExistingOutputs(string buildId, IDictionary<string, OutputFilesSnapshot> oldOutput, IEnumerable<OutputFilesSnapshot> newOutput) {
            var merger = new OutputMerger();
            //foreach (var projectOutputs in oldOutput) {
            //    if (!newOutput.ContainsKey(projectOutputs.Key)) {
            //        var outputs = projectOutputs.Value;
            //        outputs.Origin = buildId;

            //        newOutput[projectOutputs.Key] = outputs;
            //    }
            //}
        }

        public IEnumerable<BuildArtifact> WriteStateFiles(BuildOperationContext context, IEnumerable<OutputFilesSnapshot> outputs) {
            IReadOnlyCollection<BucketId> buckets = context.SourceTreeMetadata.GetBuckets();

            foreach (var bucket in buckets) {
                var tag = bucket.Tag;
                
                List<OutputFilesSnapshot> projectOutputSnapshot = new List<OutputFilesSnapshot>();
                foreach (var output in outputs) {
                    if (string.Equals(output.Directory, tag, StringComparison.OrdinalIgnoreCase)) {
                        projectOutputSnapshot.Add(output);
                    }
                }

                ArtifactCollection artifactCollection = null;
                var artifacts = context.GetArtifacts();
                if (artifacts != null) {
                    artifactCollection = artifacts.GetArtifactsForTag(tag);
                }

                BuildStateFile previousBuild = context.GetStateFile(tag);

                yield return WriteStateFile(previousBuild, bucket, projectOutputSnapshot, artifactCollection, context);
            }
        }

        private BuildArtifact WriteStateFile(BuildStateFile previousBuild, BucketId bucket, IEnumerable<OutputFilesSnapshot> projectOutputSnapshot, ArtifactCollection artifactCollection, BuildOperationContext context) {
            var pathBuilder = new ArtifactStagingPathBuilder(context.ArtifactStagingDirectory, context.BuildMetadata.BuildId, context.SourceTreeMetadata);

            string containerName = CreateContainerName(bucket.Id);

            var stateFileRoot = pathBuilder.GetBucketInstancePath(bucket.Tag);
            stateFileRoot = Path.Combine(stateFileRoot, containerName);

            var bucketInstance = Path.Combine(stateFileRoot, DefaultFileName);

            string stateFile = WriteStateFile(previousBuild, bucket, projectOutputSnapshot, artifactCollection, context.SourceTreeMetadata, context.BuildMetadata, bucketInstance);

            WrittenStateFiles.Add(stateFile);
            context.WrittenStateFiles.Add(stateFile);

            return new BuildArtifact {
                SourcePath = stateFileRoot,
                Name = containerName, /* Name must be unique within the build artifacts or TFS will complain */
                Type = VsoBuildArtifactType.FilePath
            };
        }

        /// <summary>
        /// Returns a folder name to place the state file into.
        /// </summary>
        public static string CreateContainerName(string bucketId) {
            return "~" + bucketId;
        }

        public void WriteStateFiles(IBuildPipelineServiceContract pipelineService, BuildOperationContext context) {
            var stateArtifacts = WriteStateFiles(context, pipelineService.GetAllProjectOutputs());

            pipelineService.AssociateArtifacts(stateArtifacts);

            pipelineService.Publish(context);
        }
    }
}
