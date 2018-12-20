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
        private IFileSystem fileSystem;

        public TrackedInputFilesController()
            : this(new PhysicalFileSystem(), NullLogger.Default) {
        }

        public TrackedInputFilesController(IFileSystem system, ILogger logger) {
            fileSystem = system;
            this.logger = logger;
        }

        public bool TreatInputAsFiles { get; set; } = true;

        public InputFilesDependencyAnalysisResult PerformDependencyAnalysis(ICollection<TrackedInputFile> trackedFiles, string projectSolutionRoot) {
            if (!string.IsNullOrEmpty(projectSolutionRoot)) {
                var filesToTrack = GetFilesToTrack(projectSolutionRoot);
                if (filesToTrack != null) {
                    return CorrelateInputs(filesToTrack, trackedFiles);
                }
            }

            return new InputFilesDependencyAnalysisResult(true, trackedFiles?.ToList());
        }

        internal InputFilesDependencyAnalysisResult CorrelateInputs(IReadOnlyCollection<TrackedInputFile> trackedInputFiles, ICollection<TrackedInputFile> existingTrackedFiles) {
            // Attempts to correlate inputs
            // Note: two item vector transforms may not be able to be correlated, even if they reference the same item vector, because
            // depending on the transform expression, there might be no relation between the results of the transforms

            if (existingTrackedFiles != null && existingTrackedFiles.Any()) {
                Dictionary<string, TrackedInputFile> inputFiles = CreateDictionaryFromTrackedInputFiles(trackedInputFiles);
                Dictionary<string, TrackedInputFile> existingInputFiles = CreateDictionaryFromTrackedInputFiles(existingTrackedFiles);

                List<string> commonKeys;
                List<string> uniqueKeysInTable1;
                List<string> uniqueKeysInTable2;
                DiffHashtables(inputFiles, existingInputFiles, out commonKeys, out uniqueKeysInTable1, out uniqueKeysInTable2);

                if (uniqueKeysInTable2.Count > 0 || uniqueKeysInTable1.Count > 0) {
                    return new InputFilesDependencyAnalysisResult(false, trackedInputFiles);
                }
            }

            return new InputFilesDependencyAnalysisResult(true, trackedInputFiles);
        }

        private Dictionary<string, TrackedInputFile> CreateDictionaryFromTrackedInputFiles(IEnumerable<TrackedInputFile> trackedInputFiles) {
            Dictionary<string, TrackedInputFile> dictionary = new Dictionary<string, TrackedInputFile>(StringComparer.OrdinalIgnoreCase);
            if (trackedInputFiles != null) {
                foreach (var file in trackedInputFiles) {
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

        public IReadOnlyCollection<TrackedInputFile> GetFilesToTrack(string directory) {
            var directoryPropertiesFile = Path.Combine(directory, "dir.props");

            if (fileSystem.FileExists(directoryPropertiesFile)) {
                logger.Info($"Using file: {directoryPropertiesFile} to get tracked inputs from");

                return GetFilesToTrack(directoryPropertiesFile, directory);
            }

            return null;
        }

        internal IReadOnlyCollection<TrackedInputFile> GetFilesToTrack(string directoryPropertiesFile, string directory) {
            var globalProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "SolutionRoot", directory } };

            using (var collection = new ProjectCollection(globalProps)) {
                collection.IsBuildEnabled = true;

                Project project = LoadProject(directoryPropertiesFile, collection);

                ProjectInstance projectInstance = project.CreateProjectInstance(ProjectInstanceSettings.None);

                const string target = "GenerateTrackedInputFiles";

                if (projectInstance.Targets.ContainsKey(target)) {
                    logger.Info($"Evaluating target {target} in {directoryPropertiesFile}");

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

                            List<TrackedInputFile> filesToTrack = new List<TrackedInputFile>();

                            foreach (ITaskItem item in targetResult.Items) {
                                string itemFullPath = item.GetMetadata("FullPath");

                                if (fileSystem.FileExists(itemFullPath)) {
                                    var hash = fileSystem.ComputeSha1Hash(itemFullPath);
                                    filesToTrack.Add(new TrackedInputFile(itemFullPath) { Sha1 = hash });
                                }
                            }

                            return filesToTrack;
                        }
                    }
                }
            }

            return null;
        }

        protected virtual Project LoadProject(string directoryPropertiesFile, ProjectCollection collection) {
            return collection.LoadProject(directoryPropertiesFile);
        }
    }

    internal class InputFilesDependencyAnalysisResult {
        public InputFilesDependencyAnalysisResult() {
        }

        public InputFilesDependencyAnalysisResult(bool isUpToDate)
            : this(isUpToDate, null) {
        }

        public InputFilesDependencyAnalysisResult(bool isUpToDate, IReadOnlyCollection<TrackedInputFile> trackedInputFiles) {
            IsUpToDate = isUpToDate;
            TrackedFiles = trackedInputFiles;
        }

        /// <summary>
        /// Gets a value that indicates if the inputs are up to date.
        /// </summary>
        public bool? IsUpToDate { get; }

        public IReadOnlyCollection<TrackedInputFile> TrackedFiles { get; private set; }
    }
}