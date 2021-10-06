﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private static List<BuildStateFile> GetBuildStateFiles(ILogger logger, BuildOperationContext context) {
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

                    StringBuilder sb = new StringBuilder();
                    foreach (var stateFile in files) {
                        sb.AppendFormat("Using state file: {0} -> {1} -> {2}:{3}", stateFile.Id, stateFile.BuildId, stateFile.Location, stateFile.BucketId.Tag);
                        sb.AppendLine();
                    }
                    logger.Info(sb.ToString());

                    foreach (var missingId in missingIds) {
                        logger.Info($"No state file: {missingId.Id} -> {missingId.Tag}", null);
                    }
                }

                logger.Info($"Found {files.Count} state files for {bucketCount} buckets.", null);
            }

            return files.ToList();
        }

        private static void EvictNotExistentProjects(List<BuildStateFile> stateFiles, SourceTreeMetadata sourceTreeMetadata) {
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
            const string variableName = "IsBuildCacheEnabled";

            if (stateFiles != null && stateFiles.Count > 0) {
                context.Variables[variableName] = true.ToString();
                return true;
            }
            context.Variables[variableName] = false.ToString();
            return false;
        }
    }
}