using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;

namespace Aderant.Build.ProjectSystem {
    internal class StateFileService {
        private static MemoryCache metadataCache = MemoryCache.Default;

        private readonly ILogger logger;
        private readonly IFileSystem fileSystem;

        public StateFileService(ILogger logger)
            : this(logger, new PhysicalFileSystem(null, logger)) {
        }

        private StateFileService(ILogger logger, IFileSystem physicalFileSystem) {
            this.logger = logger;
            this.fileSystem = physicalFileSystem;
        }

        /// <summary>
        /// Allows the service to process builds with an Id of 0.
        /// Useful for testing as tests do not generate a build id.
        /// </summary>
        internal bool AllowZeroBuildId { get; set; }

        public BuildStateMetadata GetBuildStateMetadata(string[] bucketIds, string[] tags, string dropLocation, BuildStateQueryOptions options, CancellationToken token = default(CancellationToken)) {
            if (bucketIds != null && tags != null && bucketIds.Length > 0 && tags.Length != 0) {
                if (bucketIds.Length != tags.Length) {
                    // The two vectors must have the same length.
                    throw new InvalidOperationException(string.Format("{2} refers to {0} item(s), and {3} refers to {1} item(s). They must have the same number of items.",
                        bucketIds.Length,
                        tags.Length,
                        "DestinationFiles",
                        "SourceFiles"));
                }
            }

            var builder = new CacheKeyBuilder();
            builder.Append(bucketIds);
            builder.Append(tags);
            builder.Append(dropLocation);
            string key = builder.ToString();

            var cachedMetadata = metadataCache.Get(key);
            var buildStateMetadata = cachedMetadata as BuildStateMetadata;
            if (buildStateMetadata != null) {
                logger.Info("Using cached build state metadata.");
                return buildStateMetadata;
            }

            var result = GetBuildStateMetadataInternal(bucketIds, dropLocation, options, token);

            metadataCache.Remove(key);
            metadataCache.Add(key, result, DateTimeOffset.UtcNow.AddMinutes(15));

            return result;
        }

        private BuildStateMetadata GetBuildStateMetadataInternal(string[] bucketIds, string dropLocation, BuildStateQueryOptions options, CancellationToken token) {
            logger.Info($"Querying prebuilt artifacts from: {dropLocation}");

            using (PerformanceTimer.Start(duration => logger.Info($"{nameof(GetBuildStateMetadata)} completed in: {duration.ToString()} ms"))) {
                var metadata = new BuildStateMetadata();

                if (bucketIds == null) {
                    return metadata;
                }

                var files = new ConcurrentQueue<BuildStateFile>();

                foreach (var bucketId in bucketIds) {
                    token.ThrowIfCancellationRequested();

                    string bucketPath = Path.Combine(dropLocation, BucketId.CreateDirectorySegment(bucketId));

                    if (fileSystem.DirectoryExists(bucketPath)) {
                        IEnumerable<string> directories = fileSystem.GetDirectories(bucketPath);

                        string[] folders = OrderBuildsByBuildNumber(directories.ToArray());

                        // Limit scanning to an arbitrary number of builds so we don't spend too
                        // long thrashing the network.
                        Parallel.ForEach(folders.Take(5), () => (BuildStateFile) null, (folder, state, stateFile) => {
                                if (state.ShouldExitCurrentIteration) {
                                    state.Stop();
                                    return null;
                                }

                                // We have to nest the state file directory as TFS won't allow duplicate artifact names
                                // For a single build we may produce 1 or more state files and so each one needs a unique artifact name
                                var stateFilePath = Path.Combine(folder, BuildStateWriter.CreateContainerName(bucketId), BuildStateWriter.DefaultFileName);

                                if (fileSystem.FileExists(stateFilePath)) {
                                    if (!fileSystem.GetDirectories(folder, false).Any()) {
                                        // If there are no directories then the state file could be
                                        // a garbage collected build in which case we should ignore it.
                                        return null;
                                    }

                                    BuildStateFile file;
                                    using (Stream stream = fileSystem.OpenFile(stateFilePath)) {
                                        file = StateFileBase.DeserializeCache<BuildStateFile>(stream);
                                    }

                                    if (file == null) {
                                        logger.Info($"Unable to deserialize file: '{stateFilePath}'.");
                                        return null;
                                    }

                                    file.Location = folder;

                                    if (IsFileTrustworthy(file, options, out var reason, out _)) {
                                        logger.Info($"Candidate-> {stateFilePath}:{reason}");
                                        return file;
                                    } else {
                                        logger.Info($"Rejected-> {stateFilePath}:{reason}");
                                    }
                                }

                                return null;
                            }, file => {
                                if (file != null) {
                                    files.Enqueue(file);
                                }
                            });
                    } else {
                        logger.Info("No prebuilt artifacts at: " + bucketPath);
                    }
                }

                metadata.BuildStateFiles = files.OrderByDescending(s => {
                    if (int.TryParse(s.BuildId, out var result)) {
                        return result;
                    }

                    return 0;
                }).ToList();

                return metadata;
            }
        }

        internal string GetReasonMessage(ArtifactCacheValidationReason reason) {
            switch (reason) {
                case ArtifactCacheValidationReason.Candidate:
                    return "Viable candidate.";
                case ArtifactCacheValidationReason.Corrupt:
                    return "Corrupt.";
                case ArtifactCacheValidationReason.NoOutputs:
                    return "No outputs.";
                case ArtifactCacheValidationReason.NoArtifacts:
                    return "No artifacts.";
                case ArtifactCacheValidationReason.BuildConfigurationMismatch:
                    return "Artifact build configuration: '{0}' does not match required configuration: '{1}'";
                default:
                    return string.Empty;
            }
        }

        internal bool IsFileTrustworthy(BuildStateFile file, BuildStateQueryOptions options, out string reason, out ArtifactCacheValidationReason validationEnum) {
            if (CheckForRootedPaths(file)) {
                validationEnum = ArtifactCacheValidationReason.Corrupt;
                reason = GetReasonMessage(validationEnum);
                return false;
            }

            // Reject files that provide no value.
            if (file.Outputs == null || file.Outputs.Count == 0) {
                validationEnum = ArtifactCacheValidationReason.NoOutputs;
                reason = GetReasonMessage(validationEnum);
                return false;
            }

            // Reject artifacts which contain no content.
            if (file.Artifacts == null || file.Artifacts.Count == 0) {
                validationEnum = ArtifactCacheValidationReason.NoArtifacts;
                reason = GetReasonMessage(validationEnum);
                return false;
            }

            // Reject artifacts if they were built with a different configuration.
            if (options != null) {
                string optionsBuildFlavor = options.BuildFlavor;

                if (!string.IsNullOrWhiteSpace(optionsBuildFlavor)) {
                    // If the build flavor is set to release, disable use of debug artifacts.
                    if (string.Equals("Release", optionsBuildFlavor, StringComparison.OrdinalIgnoreCase)) {
                        file.BuildConfiguration.TryGetValue(nameof(BuildMetadata.Flavor), out string value);

                        if (!string.IsNullOrWhiteSpace(value) && !string.Equals(optionsBuildFlavor, value, StringComparison.OrdinalIgnoreCase)) {
                            validationEnum = ArtifactCacheValidationReason.BuildConfigurationMismatch;
                            reason = string.Format(GetReasonMessage(validationEnum), value, optionsBuildFlavor);
                            return false;
                        }
                    }
                }
            }

            validationEnum = ArtifactCacheValidationReason.Candidate;
            reason = GetReasonMessage(validationEnum);

            return true;
        }

        private static bool CheckForRootedPaths(BuildStateFile file) {
            if (file.Outputs != null) {
                foreach (var key in file.Outputs.Keys) {
                    if (Path.IsPathRooted(key)) {
                        // File is corrupt and should not be used.
                        return true;
                    }
                }
            }

            return false;
        }

        internal string[] OrderBuildsByBuildNumber(string[] entries) {
            var numbers = new List<KeyValuePair<int, string>>(entries.Length);

            foreach (var entry in entries) {
                string directoryName = Path.GetFileName(entry);

                int version;
                if (Int32.TryParse(directoryName, NumberStyles.Any, CultureInfo.InvariantCulture, out version)) {
                    if (version > 0 || AllowZeroBuildId) {
                        numbers.Add(new KeyValuePair<int, string>(version, entry));
                    }
                }
            }

            return numbers.OrderByDescending(d => d.Key).Select(s => s.Value).ToArray();
        }
    }

    internal class CacheKeyBuilder {
        private StringBuilder sb;

        public CacheKeyBuilder() {
            sb = new StringBuilder();
        }

        public void Append(params string[] parts) {
            if (parts != null) {
                var sequence = parts.OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
                foreach (var part in sequence) {
                    sb.Append(part.ToUpperInvariant());
                }
            }
        }

        public override string ToString() {
            return sb.ToString();
        }
    }
}