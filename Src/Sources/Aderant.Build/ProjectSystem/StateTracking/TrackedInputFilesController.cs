using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using ILogger = Aderant.Build.Logging.ILogger;

namespace Aderant.Build.ProjectSystem.StateTracking {
    internal class TrackedInputFilesController {
        private readonly ILogger logger;
        private readonly IFileSystem fileSystem;

        /// <summary>
        /// Constructor for testing.
        /// </summary>
        internal TrackedInputFilesController() : this(new PhysicalFileSystem(), NullLogger.Default) {
        }

        public TrackedInputFilesController(IFileSystem system, ILogger logger) {
            fileSystem = system;
            this.logger = logger;
        }

        public bool TreatInputAsFiles { get; set; } = true;

        /// <summary>
        /// Instructs the build to ignore differences in NuGet packages when finding a cached build.
        /// </summary>
        public bool SkipNuGetPackageHashCheck { get; set; }


        /// <summary>
        /// Computes the up to date check result.
        /// </summary>
        /// <param name="buildStateFiles">A collection of build cache info.</param>
        /// <param name="filesToTrack">The current on disk files that should be checked for staleness</param>
        /// <param name="trackedMetadataFiles"></param>
        public InputFilesDependencyAnalysisResult PerformDependencyAnalysis(BuildStateFile[] buildStateFiles, List<TrackedInputFile> filesToTrack, IList<TrackedMetadataFile> trackedMetadataFiles) {
            var result = new InputFilesDependencyAnalysisResult();

            bool trackPackageHash;

            if (trackedMetadataFiles != null) {
                // Add metadata files to tracked files.
                if (filesToTrack == null) {
                    filesToTrack = new List<TrackedInputFile>(trackedMetadataFiles.Count);
                }

                filesToTrack.AddRange(trackedMetadataFiles);

                trackPackageHash = Convert.ToBoolean(trackedMetadataFiles.Any(x => x.TrackPackageHash));
            } else {
                trackPackageHash = false;
            }

            result.TrackedFiles = filesToTrack?.AsReadOnly();

            if (buildStateFiles != null && buildStateFiles.Length > 0) {
                Array.Sort(buildStateFiles, BuildStateFileComparer.Default);

                foreach (BuildStateFile buildStateFile in buildStateFiles) {
                    logger.Info("Assessing state file: '{0}'", buildStateFile.Id);

                    ICollection<TrackedInputFile> cachedTrackedInputFiles = buildStateFile.TrackedFiles != null ? new List<TrackedInputFile>(buildStateFile.TrackedFiles) : new List<TrackedInputFile>(1);

                    if (buildStateFile.PackageHash != null && !SkipNuGetPackageHashCheck) {
                        var syntheticFile = new TrackedMetadataFile(Constants.PaketLock) {
                            Sha1 = buildStateFile.PackageHash
                        };

                        cachedTrackedInputFiles.Add(syntheticFile);

                        logger.Info("Synthesizing tracked file {0} with hash {1}", syntheticFile.FileName, syntheticFile.Sha1);
                    }

                    if (filesToTrack?.Count != cachedTrackedInputFiles.Count) {
                        logger.Info("Tracked file count does not match");
                        continue;
                    }

                    bool skipNuGetPackageHashCheck = SkipNuGetPackageHashCheck || !trackPackageHash && !buildStateFile.TrackPackageHash;

                    if (filesToTrack.Count == 0 || CorrelateInputs(filesToTrack.AsReadOnly(), cachedTrackedInputFiles, skipNuGetPackageHashCheck)) {
                        logger.Info("Using state file");

                        result.IsUpToDate = true;
                        result.BuildStateFile = buildStateFile;

                        return result;
                    }
                }
            }

            if (filesToTrack == null || filesToTrack.Count == 0) {
                logger.Warning("No tracked files in state file.");

                result.IsUpToDate = true;
                return result;
            }

            logger.Info("No state files available.");

            result.IsUpToDate = false;
            return result;
        }

        internal bool CorrelateInputs(ICollection<TrackedInputFile> trackedInputFiles, ICollection<TrackedInputFile> cachedTrackedInputFiles, bool skipNugetPackageHashCheck) {
            // Attempts to correlate inputs
            // Note: two item vector transforms may not be able to be correlated, even if they reference the same item vector, because
            // depending on the transform expression, there might be no relation between the results of the transforms

            if (cachedTrackedInputFiles != null && cachedTrackedInputFiles.Any()) {
                Dictionary<string, TrackedInputFile> newTable = CreateDictionaryFromTrackedInputFiles(trackedInputFiles);
                Dictionary<string, TrackedInputFile> oldTable = CreateDictionaryFromTrackedInputFiles(cachedTrackedInputFiles);

                if (skipNugetPackageHashCheck) {
                    logger.Info("Package hash check disabled.");
                    var filesToRemove = new List<TrackedInputFile> {new TrackedInputFile {FileName = Constants.PaketLock}};
                    RemoveFiles(newTable, filesToRemove);
                    RemoveFiles(oldTable, filesToRemove);

                    var removedTrackedInputFiles = RemoveFilesFromPackagesFolder(newTable);
                    RemoveFiles(oldTable, removedTrackedInputFiles);
                }

                List<string> commonKeys;
                List<string> uniqueKeysInNewTable;
                List<string> uniqueKeysInOldTable;
                DiffHashTables(newTable, oldTable, out commonKeys, out uniqueKeysInNewTable, out uniqueKeysInOldTable);

                if (uniqueKeysInNewTable.Count > 0 || uniqueKeysInOldTable.Count > 0) {
                    logger.Debug($"Correlated tracked files: UniqueKeysInTable1: {uniqueKeysInNewTable.Count} | UniqueKeysInTable2:{uniqueKeysInOldTable.Count}", null);

                    foreach (string key in uniqueKeysInNewTable) {
                        TrackedInputFile inputFile = newTable[key];

                        TrackedInputFile cachedFile = oldTable.FirstOrDefault(x => string.Equals(inputFile.FileName, x.Value.FileName)).Value;

                        if (cachedFile != null) {
                            logger.Info("File is detected as modified: '{0}'. Existing file hash: '{1}', cached file hash: '{2}'", inputFile.FileName, inputFile.Sha1, cachedFile.Sha1);
                        } else {
                            logger.Info("File is detected as new: '{0}'.", inputFile.FileName);
                        }
                    }

                    return false;
                }
            }

            return true;
        }

        private static void RemoveFiles(Dictionary<string, TrackedInputFile> oldTable, List<TrackedInputFile> removedTrackedInputFiles) {
            foreach (var file in oldTable.Keys.ToList()) {
                var item = oldTable[file];

                if (item != null) {
                    foreach (var fileToRemove in removedTrackedInputFiles) {
                        if (string.Equals(item.FileName, fileToRemove.FileName, StringComparison.OrdinalIgnoreCase)) {
                            oldTable.Remove(file);
                        }
                    }
                }
            }
        }

        private static List<TrackedInputFile> RemoveFilesFromPackagesFolder(Dictionary<string, TrackedInputFile> table) {
            List<TrackedInputFile> removedTrackedFiles = new List<TrackedInputFile>();

            char[] separator = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

            foreach (var file in table.Keys.ToList()) {
                var item = table[file];

                if (item != null) {
                    var parts  = item.FullPath.Split(separator);

                    foreach (string part in parts) {
                        if (string.Equals(part, "packages", StringComparison.OrdinalIgnoreCase)) {
                            table.Remove(file);
                            removedTrackedFiles.Add(item);
                            break;
                        }
                    }
                }
            }

            return removedTrackedFiles;
        }

        private Dictionary<string, TrackedInputFile> CreateDictionaryFromTrackedInputFiles(IEnumerable<TrackedInputFile> trackedInputFiles) {
            Dictionary<string, TrackedInputFile> dictionary = new Dictionary<string, TrackedInputFile>(StringComparer.OrdinalIgnoreCase);

            if (trackedInputFiles != null) {
                foreach (var file in trackedInputFiles) {
                    if (file is TrackedMetadataFile) {
                        dictionary.Add(file.Sha1, file);

                        continue;
                    }

                    if (TreatInputAsFiles) {
                        if (file.Sha1 != null) {
                            dictionary.Add(file.Sha1, file);
                        }
                    } else {
                        dictionary.Add(file.FileName, file);
                    }

                }
            }

            return dictionary;
        }

        private static void DiffHashTables<T, V>(IDictionary<T, V> table1, IDictionary<T, V> table2, out List<T> commonKeys, out List<T> uniqueKeysInTable1, out List<T> uniqueKeysInTable2) where T : class, IEquatable<T> where V : class {
            commonKeys = new List<T>();
            uniqueKeysInTable1 = new List<T>();
            uniqueKeysInTable2 = new List<T>();

            foreach (T current in table1.Keys) {
                if (table2.ContainsKey(current)) {
                    commonKeys.Add(current);
                } else {
                    uniqueKeysInTable1.Add(current);
                }
            }

            foreach (T current2 in table2.Keys) {
                if (!table1.ContainsKey(current2)) {
                    uniqueKeysInTable2.Add(current2);
                }
            }
        }

        public virtual List<TrackedInputFile> GetFilesToTrack(string directory) {
            if (string.IsNullOrWhiteSpace(directory)) {
                return null;
            }

            var directoryPropertiesFile = Path.Combine(directory, "dir.props");

            List<TrackedInputFile> trackedInputFiles = new List<TrackedInputFile>();

            if (fileSystem.FileExists(directoryPropertiesFile)) {
                logger.Info("Using file: '{0}' to get tracked inputs from", directoryPropertiesFile);

                return trackedInputFiles.Concat(GetFilesToTrack(directoryPropertiesFile, directory)).ToList();
            }

            return trackedInputFiles;
        }

        internal IReadOnlyCollection<TrackedInputFile> GetFilesToTrack(string directoryPropertiesFile, string directory) {
            var globalProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "SolutionRoot", directory } };

            List<TrackedInputFile> filesToTrack = new List<TrackedInputFile>();

            using (var collection = new ProjectCollection(globalProps)) {
                collection.RegisterLogger(new LoggerAdapter(logger));
                collection.IsBuildEnabled = true;

                Project project = LoadProject(directoryPropertiesFile, collection);

                ProjectInstance projectInstance = project.CreateProjectInstance(ProjectInstanceSettings.None);

                const string target = "GenerateTrackedInputFiles";

                if (projectInstance.Targets.ContainsKey(target)) {
                    logger.Info($"Evaluating target {target} in {directoryPropertiesFile}", null);

                    using (BuildManager manager = new BuildManager()) {
                        var result = manager.Build(
                            new BuildParameters(collection) { EnableNodeReuse = false },
                            new BuildRequestData(
                                projectInstance,
                                new[] { target },
                                null,
                                BuildRequestDataFlags.ProvideProjectStateAfterBuild));

                        if (result.OverallResult == BuildResultCode.Failure) {
                            throw new Exception("Failed to evaluate: " + target, result.Exception);
                        }

                        if (result.HasResultsForTarget(target)) {
                            TargetResult targetResult = result.ResultsByTarget[target];

                            foreach (ITaskItem item in targetResult.Items) {
                                string itemFullPath = item.GetMetadata("FullPath");

                                // If globbing evaluation failed, then ignore the path
                                if (!itemFullPath.Contains("**")) {
                                    if (fileSystem.FileExists(itemFullPath)) {
                                        var hash = fileSystem.ComputeSha1Hash(itemFullPath);
                                        filesToTrack.Add(new TrackedInputFile(itemFullPath) { Sha1 = hash });
                                    }
                                }
                            }

                            return filesToTrack;
                        }
                    }
                }
            }

            return filesToTrack;
        }

        protected virtual Project LoadProject(string directoryPropertiesFile, ProjectCollection collection) {
            return collection.LoadProject(directoryPropertiesFile);
        }

        internal class LoggerAdapter : Microsoft.Build.Framework.ILogger {
            private readonly ILogger logger;
            private IEventSource source;

            public LoggerAdapter(ILogger logger) {
                this.logger = logger;

            }

            public void Initialize(IEventSource eventSource) {
                source = eventSource;

                eventSource.MessageRaised += OnMessageRaised;
                eventSource.ErrorRaised += OnErrorRaised;
                eventSource.WarningRaised += OnWarningRaised;
            }

            private void OnWarningRaised(object sender, BuildWarningEventArgs args) {
                logger.Warning(args.Message, null);
            }

            private void OnErrorRaised(object sender, BuildErrorEventArgs args) {
                logger.Error(args.Message, null);
            }

            private void OnMessageRaised(object sender, BuildMessageEventArgs args) {
                logger.Info(args.Message, null);
            }

            public void Shutdown() {
                source.MessageRaised -= OnMessageRaised;
                source.ErrorRaised -= OnErrorRaised;
                source.WarningRaised -= OnWarningRaised;
            }

            public LoggerVerbosity Verbosity { get; set; }
            public string Parameters { get; set; }
        }
    }

    internal class InputFilesDependencyAnalysisResult {

        /// <summary>
        /// Gets a value that indicates if the inputs are up to date.
        /// </summary>
        public bool? IsUpToDate { get; internal set; }

        public IReadOnlyCollection<TrackedInputFile> TrackedFiles { get; internal set; }

        internal BuildStateFile BuildStateFile { get; set; }
    }
}
