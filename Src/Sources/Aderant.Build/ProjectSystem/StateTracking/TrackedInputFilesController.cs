using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.PowerShell.Commands;
using ILogger = Aderant.Build.Logging.ILogger;

namespace Aderant.Build.ProjectSystem.StateTracking {
    internal class TrackedInputFilesController {
        private readonly ILogger logger;
        private IFileSystem fileSystem;

        public TrackedInputFilesController()
            : this(new PhysicalFileSystem(), NullLogger.Default) {
        }

        public TrackedInputFilesController(IFileSystem system, ILogger logger) {
            fileSystem = system;
            this.logger = logger;
        }

        public bool TreatInputAsFiles { get; set; } = true;

        /// <summary>
        /// Instructs the build to ignore differences in NuGet packages when finding a cached build.
        /// </summary>
        public bool SkipNugetPackageHashCheck { get; set; }

        public InputFilesDependencyAnalysisResult PerformDependencyAnalysis(ICollection<TrackedInputFile> trackedFiles, string projectSolutionRoot, string packageHash) {
            InputFilesDependencyAnalysisResult result = new InputFilesDependencyAnalysisResult();

            if (!string.IsNullOrEmpty(projectSolutionRoot)) {
                var filesToTrack = GetFilesToTrack(projectSolutionRoot);

                if (filesToTrack != null) {
                    CorrelateInputs(result, filesToTrack, trackedFiles, packageHash);
                }

                return result;
            }

            result.IsUpToDate = true;
            result.TrackedFiles = trackedFiles?.ToList();

            return result;
        }

        internal void CorrelateInputs(InputFilesDependencyAnalysisResult result, IReadOnlyCollection<TrackedInputFile> trackedInputFiles, ICollection<TrackedInputFile> existingTrackedFiles, string packageHash) {
            // Attempts to correlate inputs
            // Note: two item vector transforms may not be able to be correlated, even if they reference the same item vector, because
            // depending on the transform expression, there might be no relation between the results of the transforms

            if (!string.IsNullOrWhiteSpace(packageHash)) {
                if (existingTrackedFiles == null) {
                    existingTrackedFiles = new List<TrackedInputFile>(1);
                }

                existingTrackedFiles.Add(new TrackedMetadataFile(Constants.PaketLock) { Sha1 = packageHash });
            }

            if (existingTrackedFiles != null && existingTrackedFiles.Any()) {
                Dictionary<string, TrackedInputFile> newTable = CreateDictionaryFromTrackedInputFiles(trackedInputFiles);
                Dictionary<string, TrackedInputFile> oldTable = CreateDictionaryFromTrackedInputFiles(existingTrackedFiles);

                if (SkipNugetPackageHashCheck) {
                    RemoveLockFile(newTable);
                    RemoveLockFile(oldTable);
                }

                List<string> commonKeys;
                List<string> uniqueKeysInNewTable;
                List<string> uniqueKeysInOldTable;
                DiffHashtables(newTable, oldTable, out commonKeys, out uniqueKeysInNewTable, out uniqueKeysInOldTable);

                if (uniqueKeysInNewTable.Count > 0 || uniqueKeysInOldTable.Count > 0) {
                    logger.Debug($"Correlated tracked files: UniqueKeysInTable1: {uniqueKeysInNewTable.Count} | UniqueKeysInTable2:{uniqueKeysInOldTable.Count}", null);

                    foreach (var key in uniqueKeysInNewTable) {
                        TrackedInputFile inputFile = newTable[key];
                        logger.Info($"File is detected as modified or new: {inputFile.FileName}", null);
                    }

                    result.IsUpToDate = false;
                    result.TrackedFiles = trackedInputFiles;

                    return;
                }
            }

            result.IsUpToDate = true;
            result.TrackedFiles = trackedInputFiles;
        }

        private void RemoveLockFile(Dictionary<string, TrackedInputFile> table) {
            foreach (var file in table.Keys.ToList()) {
                var item = table[file];
                if (string.Equals(item.FileName, Constants.PaketLock, StringComparison.OrdinalIgnoreCase)) {
                    table.Remove(file);
                }
            }
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

        private static void DiffHashtables<T, V>(IDictionary<T, V> table1, IDictionary<T, V> table2, out List<T> commonKeys, out List<T> uniqueKeysInTable1, out List<T> uniqueKeysInTable2) where T : class, IEquatable<T> where V : class {
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

        public virtual IReadOnlyCollection<TrackedInputFile> GetFilesToTrack(string directory) {
            var directoryPropertiesFile = Path.Combine(directory, "dir.props");

            // Get paket.lock if it exists
            string paketLockFile = Path.Combine(directory, Constants.PaketLock);

            List<TrackedInputFile> trackedInputFiles = new List<TrackedInputFile>();

            if (fileSystem.FileExists(paketLockFile)) {
                PaketLockOperations paketLockOperations = new PaketLockOperations(paketLockFile, fileSystem.ReadAllLines(paketLockFile));

                string packageHash = PaketLockOperations.HashLockFile(paketLockOperations.LockFileContent);

                trackedInputFiles.Add(new TrackedMetadataFile(paketLockFile) {
                    PackageHash = packageHash,
                    PackageGroups = paketLockOperations.GetPackageInfo(),
                    Sha1 = packageHash
                });
            }

            if (fileSystem.FileExists(directoryPropertiesFile)) {
                logger.Info($"Using file: {directoryPropertiesFile} to get tracked inputs from", null);

                return trackedInputFiles.Concat(GetFilesToTrack(directoryPropertiesFile, directory)).ToList().AsReadOnly();
            }

            if (trackedInputFiles.Count == 0) {
                return null;
            }

            return trackedInputFiles.AsReadOnly();
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

            public LoggerAdapter(ILogger logger) {
                this.logger = logger;

            }

            public void Initialize(IEventSource eventSource) {
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
            }

            public LoggerVerbosity Verbosity { get; set; }
            public string Parameters { get; set; }
        }
    }

    internal class InputFilesDependencyAnalysisResult {

        public InputFilesDependencyAnalysisResult() {
        }

        public InputFilesDependencyAnalysisResult(bool isUpToDate, IReadOnlyCollection<TrackedInputFile> trackedInputFiles) {
            IsUpToDate = isUpToDate;
            TrackedFiles = trackedInputFiles;
        }

        /// <summary>
        /// Gets a value that indicates if the inputs are up to date.
        /// </summary>
        public bool? IsUpToDate { get; internal set; }

        public IReadOnlyCollection<TrackedInputFile> TrackedFiles { get; internal set; }
    }
}
