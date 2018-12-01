﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Utilities;

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

        public IList<PathSpec> BuildRestoreOperations(string solutionRoot, string container, BuildStateFile stateFile) {
            HashSet<string> destinationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<PathSpec> copyOperations = new List<PathSpec>();

            var projectOutputs = stateFile.Outputs.Where(o => string.Equals(o.Value.Directory, container, StringComparison.OrdinalIgnoreCase)).ToList();

            LocalArtifactFileComparer localArtifactComparer = new LocalArtifactFileComparer();

            foreach (var project in projectOutputs) {
                ErrorUtilities.IsNotNull(project.Value.OutputPath, nameof(project.Value.OutputPath));

                var projectFile = NormalizePath(project);

                // Adjust the source relative path to a solution relative path
                string localProjectFile = Path.Combine(solutionRoot, projectFile);
                var directoryOfProject = Path.GetDirectoryName(localProjectFile);

                if (fileSystem.FileExists(localProjectFile)) {
                    logger.Info($"Calculating files to restore for project: {Path.GetFileNameWithoutExtension(project.Key)}");

                    foreach (var fileWrite in project.Value.FilesWritten) {
                        // Retain the relative path of the build artifact.
                        string filePath = fileWrite.Replace(project.Value.OutputPath, "", StringComparison.OrdinalIgnoreCase);

                        // Use relative path for comparison.
                        List<LocalArtifactFile> localSourceFiles = localArtifactFiles.Where(s => s.FullPath.EndsWith(string.Concat(@"\", filePath), StringComparison.OrdinalIgnoreCase)).ToList();

                        if (localSourceFiles.Count == 0) {
                            continue;
                        }

                        List<LocalArtifactFile> distinctLocalSourceFiles = localSourceFiles.Distinct(localArtifactComparer).ToList();
                        if (localSourceFiles.Count > distinctLocalSourceFiles.Count) {
                            // Log duplicates.
                            IEnumerable<LocalArtifactFile> duplicateArtifacts = localSourceFiles.GroupBy(x => x, localArtifactComparer).Where(group => group.Count() > 1).Select(group => group.Key);

                            string duplicates = string.Join(Environment.NewLine, duplicateArtifacts);
                            logger.Error($"File {filePath} exists in more than one artifact." + Environment.NewLine + duplicates);
                            break;
                        }


                        // There can be only one.
                        LocalArtifactFile artifactFile = distinctLocalSourceFiles.First();

                        string destination = Path.GetFullPath(Path.Combine(directoryOfProject, project.Value.OutputPath, filePath));
                        AddFileDestination(
                            destinationPaths,
                            artifactFile,
                            destination,
                            copyOperations);

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

        private void AddFileDestination(HashSet<string> destinationPaths, LocalArtifactFile file, string destination, List<PathSpec> copyOperations) {
            if (destinationPaths.Add(destination)) {
                logger.Info($"Selected artifact file: {file.FullPath}");
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