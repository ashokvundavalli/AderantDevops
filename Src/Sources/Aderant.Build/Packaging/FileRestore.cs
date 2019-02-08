using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;

namespace Aderant.Build.Packaging {
    class LocalArtifactFileComparer : IEqualityComparer<LocalArtifactFile> {
        public bool Equals(LocalArtifactFile x, LocalArtifactFile y) {
            if (x == null) {
                throw new ArgumentNullException(nameof(x));
            }

            if (y == null) {
                throw new ArgumentNullException(nameof(y));
            }

            return string.Equals(x.FullPath, y.FullPath, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(LocalArtifactFile obj) {
            return obj.GetHashCode();
        }
    }

    internal class FileRestore {
        private readonly IFileSystem fileSystem;
        private readonly List<LocalArtifactFile> localArtifactFiles;
        private readonly ILogger logger;
        private readonly IBuildPipelineService pipelineService;
        private List<ProjectOutputSnapshot> webProjects = new List<ProjectOutputSnapshot>();

        public FileRestore(List<LocalArtifactFile> localArtifactFiles, IBuildPipelineService pipelineService, IFileSystem fileSystem, ILogger logger) {
            this.localArtifactFiles = localArtifactFiles;
            this.pipelineService = pipelineService;
            this.fileSystem = fileSystem;
            this.logger = logger;
        }

        public string CommonOutputDirectory { get; set; }

        public string CommonDependencyDirectory { get; set; }
        public IReadOnlyCollection<string> StagingDirectoryWhitelist { get; set; }

        private static readonly string[] knownOutputPaths = new string[] {
            @"Bin\Module\",
            @"Bin\Test\"
        };

        internal static string CalculateRestorePath(string fileWrite, string outputPath) {
            int index = fileWrite.IndexOf(outputPath, StringComparison.OrdinalIgnoreCase);
            if (index == 0) {
                // Retain the relative path of the build artifact.
                return fileWrite.Remove(index, outputPath.Length);
            }

            // TODO: Remove this
            // If the file path matches any known output paths, proceed.
            foreach (string knownOutputPath in knownOutputPaths) {
                index = fileWrite.IndexOf(knownOutputPath, StringComparison.OrdinalIgnoreCase);

                if (index != -1) {
                    return fileWrite.Remove(0, index + knownOutputPath.Length);
                }
            }

            return null;
        }

        public IList<PathSpec> BuildRestoreOperations(string solutionRoot, string container, BuildStateFile stateFile) {
            // Guard to ensure only one write to a destination occurs
            HashSet<string> seenDestinationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            //  The source/destination mapping
            List<PathSpec> copyOperations = new List<PathSpec>();

            var projectOutputs = stateFile.Outputs
                .Where(o => string.Equals(o.Value.Directory, container, StringComparison.OrdinalIgnoreCase))
                .ToList();

            LocalArtifactFileComparer localArtifactComparer = new LocalArtifactFileComparer();

            foreach (var project in projectOutputs) {
                ErrorUtilities.IsNotNull(project.Value.OutputPath, nameof(project.Value.OutputPath));

                string projectFile = NormalizePath(project);

                // Adjust the source relative path to a solution relative path
                string localProjectFile = Path.Combine(solutionRoot, projectFile);
                string directoryOfProject = Path.GetDirectoryName(localProjectFile);
                string outputPath = project.Value.OutputPath.NormalizeTrailingSlashes();

                bool isTestProject = project.Value.IsTestProject;
                if (isTestProject) {
                    // Because web projects are test projects in some cases it is critical to not incorrectly treat them as purely test projects
                    isTestProject = !webProjects.Contains(project.Value);
                }

                List<string> targetDirectories = CreateDestinationDirectoryList(directoryOfProject, outputPath, isTestProject);

                if (fileSystem.FileExists(localProjectFile)) {
                    logger.Info($"Calculating files to restore for project: {Path.GetFileNameWithoutExtension(project.Key)}");

                    foreach (string fileWrite in project.Value.FilesWritten) {
                        string filePath = CalculateRestorePath(fileWrite, outputPath);

                        // Use relative path for comparison.
                        List<LocalArtifactFile> localSourceFiles = localArtifactFiles.Where(s => s.FullPath.EndsWith(string.Concat(@"\", filePath), StringComparison.OrdinalIgnoreCase)).ToList();

                        if (localSourceFiles.Count == 0) {
                            if (filePath != null) {
                                logger.Warning($"{localProjectFile}: Could not locate '{filePath}' in build cache.");
                            }
                            continue;
                        }

                        List<LocalArtifactFile> distinctLocalSourceFiles = localSourceFiles.Distinct(localArtifactComparer).ToList();
                        if (localSourceFiles.Count > distinctLocalSourceFiles.Count) {
                            // Log duplicates.
                            IEnumerable<LocalArtifactFile> duplicateArtifacts = localSourceFiles.GroupBy(x => x, localArtifactComparer).Where(group => @group.Count() > 1).Select(group => @group.Key);

                            string duplicates = string.Join(Environment.NewLine, duplicateArtifacts);
                            logger.Error($"File {filePath} exists in more than one artifact." + Environment.NewLine + duplicates);
                            break;
                        }

                        // There can be only one.
                        LocalArtifactFile artifactFile = distinctLocalSourceFiles.First();

                        CheckWhitelistForFile(filePath, targetDirectories);

                        int copyCount = copyOperations.Count;
                        foreach (string targetDirectory in targetDirectories) {
                            string destination = Path.GetFullPath(Path.Combine(targetDirectory, filePath));

                            AddFileDestination(
                                seenDestinationPaths,
                                artifactFile,
                                destination,
                                copyOperations);
                        }

                        if (!string.IsNullOrWhiteSpace(CommonOutputDirectory)) {
                            if (webProjects.Contains(project.Value)) {
                                AddFileDestination(
                                    seenDestinationPaths,
                                    artifactFile,
                                    Path.GetFullPath(Path.Combine(CommonOutputDirectory, filePath)),
                                    copyOperations);
                            }
                        }

                        if (copyOperations.Count == copyCount) {
                            logger.Info($"Ignored file: {localProjectFile}: '{fileWrite}'");
                        }
                    }
                } else {
                    throw new FileNotFoundException($"The file {localProjectFile} does not exist or cannot be accessed.", localProjectFile);
                }
            }

            return copyOperations;
        }

        // Test if the user has explicitly allowed the file via customization
        private void CheckWhitelistForFile(string filePath, List<string> targetDirectories) {
            if (StagingDirectoryWhitelist != null && !string.IsNullOrWhiteSpace(CommonDependencyDirectory)) {
                foreach (var item in StagingDirectoryWhitelist) {
                    if (WildcardPattern.ContainsWildcardCharacters(item)) {
                        WildcardPattern pattern = WildcardPattern.Get(item, WildcardOptions.IgnoreCase);

                        if (pattern.IsMatch(filePath)) {
                            logger.Debug("File '{0}' was included because of: {1}", filePath, pattern);
                            targetDirectories.Add(CommonDependencyDirectory);
                            break;
                        }
                    }
                }
            }
        }

        private List<string> CreateDestinationDirectoryList(string directoryOfProject, string outputPath, bool isTestProject) {
            // The destination folder paths
            var targetPaths = new List<string>();
            targetPaths.Add(Path.GetFullPath(Path.Combine(directoryOfProject, outputPath)));

            if (!isTestProject) {
                if (CommonDependencyDirectory != null) {
                    targetPaths.Add(CommonDependencyDirectory);
                }
            }

            return targetPaths;
        }

        /// <param name="destinationPaths">The complete set of paths to written to. Used to prevent double writes.</param>
        /// <param name="file">The file downloaded from the artifact cache.</param>
        /// <param name="destination">The destination for <paramref name="file"/></param>
        /// <param name="copyOperations">The copy operations</param>
        private void AddFileDestination(HashSet<string> destinationPaths, LocalArtifactFile file, string destination, List<PathSpec> copyOperations) {
            if (destinationPaths.Add(destination)) {
                copyOperations.Add(new PathSpec(file.FullPath, destination));
            } else {
                logger.Warning("Double write for file: " + destination);
            }
        }

        private static string NormalizePath(KeyValuePair<string, ProjectOutputSnapshot> project) {
            string projectFile = project.Key.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return projectFile;
        }

        public void DetermineAdditionalRestorePaths(BuildStateFile stateFile) {
            if (pipelineService == null) {
                return;
            }

            IEnumerable<OnDiskProjectInfo> projects = pipelineService.GetTrackedProjects(stateFile.GetProjectGuids());

            foreach (var project in projects) {
                if (project.IsWebProject.GetValueOrDefault()) {

                    foreach (var item in stateFile.Outputs) {
                        if (item.Value.ProjectGuid == project.ProjectGuid) {
                            webProjects.Add(item.Value);
                        }
                    }
                }
            }
        }
    }
}
