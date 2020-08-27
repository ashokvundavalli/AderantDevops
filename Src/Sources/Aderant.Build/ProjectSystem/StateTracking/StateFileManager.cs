using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Logging;
using Aderant.Build.VersionControl;
using Aderant.Build.VersionControl.Model;

namespace Aderant.Build.ProjectSystem.StateTracking {
    internal class StateFileController {
        public List<BuildStateFile> GetApplicableStateFiles(ILogger logger, BuildOperationContext context) {
            var files = GetBuildStateFiles(logger, context);

            if (files != null) {
                EvictNotExistentProjects(files, context.SourceTreeMetadata);
            }

            return files;
        }

        private List<BuildStateFile> GetBuildStateFiles(ILogger logger, BuildOperationContext context) {
            List<BucketId> missingIds;

            IList<BuildStateFile> files = new List<BuildStateFile>();

            // Here we select an appropriate tree to reuse
            var buildStateMetadata = context.BuildStateMetadata;

            int bucketCount = -1;

            if (buildStateMetadata != null && context.SourceTreeMetadata != null) {
                if (buildStateMetadata.BuildStateFiles != null) {
                    IReadOnlyCollection<BucketId> buckets = context.SourceTreeMetadata.GetBuckets();

                    bucketCount = buckets.Count;

                    files = buildStateMetadata.QueryCacheForBuckets(buckets, out missingIds);

                    foreach (var stateFile in files) {
                        logger.Info($"Using state file: {stateFile.Id} -> {stateFile.BuildId} -> {stateFile.Location}:{stateFile.BucketId.Tag}", null);
                    }

                    foreach (var missingId in missingIds) {
                        logger.Info($"No state file: {missingId.Id} -> {missingId.Tag}", null);
                    }
                }

                logger.Info($"Found {files.Count}/{bucketCount} state files.", null);
            }

            return files.ToList();
        }

        private void EvictNotExistentProjects(List<BuildStateFile> stateFiles, SourceTreeMetadata sourceTreeMetadata) {
            if (sourceTreeMetadata == null) {
                return;
            }

            // here we evict deleted projects from the previous builds metadata
            // This is so we do not consider the outputs of this project in the artifact restore phase
            if (sourceTreeMetadata.Changes != null) {
                var changes = sourceTreeMetadata.Changes;

                foreach (var sourceChange in changes) {
                    if (sourceChange.Status == FileStatus.Deleted) {
                        foreach (var file in stateFiles) {
                            if (file.Outputs.ContainsKey(sourceChange.Path)) {
                                file.Outputs.Remove(sourceChange.Path);
                            }
                        }
                    }
                }
            }
        }

        public static bool SetIsBuildCacheEnabled(List<BuildStateFile> stateFiles, BuildOperationContext context) {
            if (stateFiles != null && stateFiles.Count > 0) {
                context.Variables["IsBuildCacheEnabled"] = true.ToString();
                return true;
            }

            return false;
        }
    }
}