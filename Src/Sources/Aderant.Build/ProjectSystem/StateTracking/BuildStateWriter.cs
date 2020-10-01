using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.AzurePipelines;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.PipelineService;
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

        public static string DefaultFileName { get; } = "buildstate.metadata";

        public ICollection<string> WrittenStateFiles { get; set; }

        /// <summary>
        /// Serializes the build state file
        /// </summary>
        /// <param name="previousBuild">The state file selected to build with.</param>
        /// <param name="bucket">The SHA of the directory for which the outputs are associated</param>
        /// <param name="currentOutputs">The outputs the build produced</param>
        /// <param name="artifacts">The artifacts this build produced</param>
        /// <param name="sourceTreeMetadata">A description of the source tree</param>
        /// <param name="buildMetadata">A description of the current build environment</param>
        /// <param name="destinationPath">The path to write the file to</param>
        /// <param name="rootDirectory"></param>
        public string WriteStateFile(
            BuildStateFile previousBuild,
            BucketId bucket,
            IEnumerable<ProjectOutputSnapshot> currentOutputs,
            ICollection<TrackedInputFile> trackedInputFiles,
            IDictionary<string, ICollection<ArtifactManifest>> artifacts,
            SourceTreeMetadata sourceTreeMetadata,
            BuildMetadata buildMetadata,
            string destinationPath,
            string rootDirectory) {

            BuildStateFile newFile;
            return WriteStateFile(previousBuild, bucket, currentOutputs, trackedInputFiles, artifacts, sourceTreeMetadata, buildMetadata, destinationPath, rootDirectory, out newFile);
        }

        internal string WriteStateFile(
            BuildStateFile previousBuild,
            BucketId bucket,
            IEnumerable<ProjectOutputSnapshot> currentOutputs,
            ICollection<TrackedInputFile> trackedInputFiles,
            IDictionary<string, ICollection<ArtifactManifest>> artifacts,
            SourceTreeMetadata sourceTreeMetadata,
            BuildMetadata buildMetadata,
            string destinationPath,
            string rootDirectory,
            out BuildStateFile stateFile) {

            string treeShaValue = null;
            if (sourceTreeMetadata != null) {
                var treeSha = sourceTreeMetadata.GetBucket(BucketId.Current);
                treeShaValue = treeSha.Id;
            }

            stateFile = new BuildStateFile {
                TreeSha = treeShaValue,
                BucketId = bucket
            };

            if (previousBuild != null) {
                stateFile.ParentId = previousBuild.Id;
                stateFile.ParentBuildId = previousBuild.BuildId;
                stateFile.ParentTreeSha = previousBuild.TreeSha;
            }

            if (currentOutputs == null) {
                currentOutputs = Enumerable.Empty<ProjectOutputSnapshot>();
            }

            if (previousBuild != null) {
                //TODO: Can we get away with the merge in the ArtifactService?
                //MergeExistingOutputs(previousBuild.BuildId, previousBuild.Outputs, currentOutputs);
            }

            // Set build configuration.
            if (buildMetadata != null) {
                stateFile.BuildConfiguration = new Dictionary<string, string> {
                    {nameof(BuildMetadata.Flavor), buildMetadata.Flavor}
                };

                stateFile.BuildId = buildMetadata.BuildId.ToString(CultureInfo.InvariantCulture);

                if (buildMetadata.IsPullRequest) {
                    stateFile.PullRequestInfo = buildMetadata.PullRequest;
                } else {
                    stateFile.ScmBranch = buildMetadata.ScmBranch;
                    stateFile.ScmCommitId = buildMetadata.ScmCommitId;
                }
            }

            stateFile.Outputs = currentOutputs.ToDictionary(key => key.ProjectFile, value => value);

            // If there are no outputs and no associated artifacts, avoid writing the state file.
            if ((stateFile.Outputs == null || stateFile.Outputs.Count == 0) && (artifacts == null || artifacts.Count == 0 || artifacts.All(x => x.Value.Count == 0))) {
                return null;
            }

            RemoveTransitiveBaggage(stateFile.Outputs);

            stateFile.Artifacts = artifacts;
            if (trackedInputFiles != null) {
                var trackedMetadataFiles = trackedInputFiles.Where(x => x is TrackedMetadataFile).ToList();

                foreach (var trackedMetadataFile in trackedMetadataFiles) {
                    trackedMetadataFile.EnrichStateFile(stateFile);
                }

                // Filter out metadata files.
                stateFile.TrackedFiles = trackedInputFiles.Except(trackedMetadataFiles).ToList();
            }

            stateFile.PrepareForSerialization();

            var file = stateFile;

            logger.Info("Writing state file to: " + destinationPath);
            fileSystem.AddFile(destinationPath, stream => file.Serialize(stream));

            return destinationPath;
        }

        private void RemoveTransitiveBaggage(IDictionary<string, ProjectOutputSnapshot> stateFileOutputs) {
            // Gather up all unique dir+output pairs
            foreach (var projects in stateFileOutputs.Values
                .Where(g => g.IsTestProject)
                .GroupBy(g => g.Directory + g.OutputPath, StringComparer.OrdinalIgnoreCase)) {

                // All of the output
                var seenOutputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var project in projects) {
                    if (project.FilesWritten != null) {
                        var outputsWithoutTransitiveBaggage = new List<string>(project.FilesWritten.Length);

                        foreach (var file in project.FilesWritten) {
                            if (seenOutputFiles.Add(file)) {
                                // Add was accepted, this is a unique file
                                outputsWithoutTransitiveBaggage.Add(file);
                            }
                        }

                        project.FilesWritten = outputsWithoutTransitiveBaggage.ToArray();
                    }
                }
            }
        }

        private static void MergeExistingOutputs(string buildId, IDictionary<string, ProjectOutputSnapshot> oldOutput, IEnumerable<ProjectOutputSnapshot> newOutput) {
            var merger = new OutputMerger();
            //foreach (var projectOutputs in oldOutput) {s
            //    if (!newOutput.ContainsKey(projectOutputs.Key)) {
            //        var outputs = projectOutputs.Value;
            //        outputs.Origin = buildId;

            //        newOutput[projectOutputs.Key] = outputs;
            //    }
            //}
        }

        public IEnumerable<BuildArtifact> WriteStateFiles(BuildOperationContext context, IEnumerable<ProjectOutputSnapshot> outputs, IBuildPipelineService service) {
            IReadOnlyCollection<BucketId> buckets = context.SourceTreeMetadata.GetBuckets();

            var files = new List<BuildArtifact>();

            foreach (var bucket in buckets) {
                var tag = bucket.Tag;

                List<ProjectOutputSnapshot> projectOutputSnapshot = new List<ProjectOutputSnapshot>();
                foreach (var output in outputs) {
                    if (string.Equals(output.Directory, tag, StringComparison.OrdinalIgnoreCase)) {
                        projectOutputSnapshot.Add(output);
                    }
                }

                BuildStateFile previousBuild = context.GetStateFile(tag);

                var artifactManifests = service.GetArtifactsForContainer(tag);

                var trackedInputFiles = service.ClaimTrackedInputFiles(tag);

                if (trackedInputFiles != null) {
                    logger.Info($"Claimed {trackedInputFiles.Count} tracked files for:" + tag);
                }

                var collection = new ArtifactCollection();
                if (artifactManifests != null) {
                    collection[tag] = artifactManifests.ToList();
                }

                BuildArtifact buildArtifact = WriteStateFile(previousBuild, bucket, trackedInputFiles?.ToList(), projectOutputSnapshot, collection, context);

                if (buildArtifact == null) {
                    continue;
                }

                files.Add(buildArtifact);
            }

            return files;
        }

        private BuildArtifact WriteStateFile(BuildStateFile previousBuild, BucketId bucket, ICollection<TrackedInputFile> trackedInputFiles, IEnumerable<ProjectOutputSnapshot> projectOutputSnapshot, ArtifactCollection artifactCollection, BuildOperationContext context) {
            ArtifactStagingPathBuilder pathBuilder = new ArtifactStagingPathBuilder(context.ArtifactStagingDirectory, context.BuildMetadata.BuildId, context.SourceTreeMetadata);

            string containerName = CreateContainerName(bucket.Id);

            bool sendToArtifactCache;
            string stateFileRoot = pathBuilder.CreatePath(bucket.Tag, out sendToArtifactCache);

            if (stateFileRoot == null) {
                logger.Info($"No path for {bucket.Tag} was generated.");
                return null;
            }

            stateFileRoot = Path.Combine(stateFileRoot, containerName);

            string bucketInstance = Path.Combine(stateFileRoot, DefaultFileName);

            string stateFile = WriteStateFile(previousBuild, bucket, projectOutputSnapshot, trackedInputFiles, artifactCollection, context.SourceTreeMetadata, context.BuildMetadata, bucketInstance, context.BuildRoot);

            if (string.IsNullOrWhiteSpace(stateFile)) {
                return null;
            }

            AddStateFileWrite(context, stateFile);

            return new BuildArtifact(containerName) { // Artifact name must be unique within the build artifacts or TFS will complain.
                SourcePath = stateFileRoot,
                Name = containerName, 
                Type = VsoBuildArtifactType.FilePath,
                SendToArtifactCache = sendToArtifactCache
            };
        }

        private void AddStateFileWrite(BuildOperationContext context, string stateFile) {
            WrittenStateFiles.Add(stateFile);
            context.WrittenStateFiles.Add(stateFile);
        }

        /// <summary>
        /// Returns a folder name to place the state file into.
        /// </summary>
        public static string CreateContainerName(string bucketId) {
            return "~" + bucketId;
        }

        public void WriteStateFiles(IBuildPipelineService pipelineService, BuildOperationContext context) {
            var stateArtifacts = WriteStateFiles(context, pipelineService.GetProjectSnapshots(), pipelineService);

            pipelineService.AssociateArtifacts(stateArtifacts);

            pipelineService.Publish(context);
        }
    }
}
