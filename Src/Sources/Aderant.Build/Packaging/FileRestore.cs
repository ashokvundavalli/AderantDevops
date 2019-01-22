using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public IReadOnlyCollection<string> AdditionalDestinationDirectories { get; set; }

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
            HashSet<string> destinationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                string outputPath = project.Value.OutputPath.NormalizePath();

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

                        foreach (string targetDirectory in targetDirectories) {
                            string destination = Path.GetFullPath(Path.Combine(targetDirectory, filePath));

                            AddFileDestination(
                                destinationPaths,
                                artifactFile,
                                destination,
                                copyOperations);
                        }

                        if (string.IsNullOrWhiteSpace(filePath)) {
                            OnDiskProjectInfo projectInfo = BuildPipelineServiceClient.Current.GetTrackedProjects(new List<Guid> {
                                project.Value.ProjectGuid
                            }).FirstOrDefault();

                            if (projectInfo != null) {
                                if (!string.IsNullOrWhiteSpace(projectInfo.DesktopBuildPackageLocation)) {
                                    if (fileWrite.Equals(projectInfo.DesktopBuildPackageLocation, StringComparison.OrdinalIgnoreCase)) {
                                        string targetPath = Path.GetFullPath(Path.Combine(directoryOfProject, fileWrite));

                                        AddFileDestination(new HashSet<string> { targetPath }, artifactFile, targetPath, copyOperations);
                                    }
                                }
                            }

                            logger.Info($"Ignored file: {localProjectFile}: '{fileWrite}' does not start with project output path '{outputPath}'.");
                        }

                        if (!string.IsNullOrWhiteSpace(CommonOutputDirectory)) {
                            if (webProjects.Contains(project.Value)) {
                                AddFileDestination(
                                    destinationPaths,
                                    artifactFile,
                                    Path.GetFullPath(Path.Combine(CommonOutputDirectory, filePath)),
                                    copyOperations);
                            }
                        }
                    }
                } else {
                    throw new FileNotFoundException($"The file {localProjectFile} does not exist or cannot be accessed.", localProjectFile);
                }
            }

            return copyOperations;
        }

        private List<string> CreateDestinationDirectoryList(string directoryOfProject, string outputPath, bool isTestProject) {
            // The destination folder paths
            var targetPaths = new List<string>();
            targetPaths.Add(Path.GetFullPath(Path.Combine(directoryOfProject, outputPath)));

            if (!isTestProject) {
                if (AdditionalDestinationDirectories != null) {
                    targetPaths.AddRange(AdditionalDestinationDirectories);
                }
            }

            return targetPaths;
        }

        private void AddFileDestination(HashSet<string> destinationPaths, LocalArtifactFile file, string destination, List<PathSpec> copyOperations) {
            if (destinationPaths.Add(destination)) {
                logger.Info($"Selected artifact file: '{file.FullPath}' to copy to: '{destination}'.");
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
