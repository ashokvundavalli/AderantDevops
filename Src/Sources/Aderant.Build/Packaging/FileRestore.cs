using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Debugger = System.Diagnostics.Debugger;

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

        public FileRestore(List<LocalArtifactFile> localArtifactFiles, IBuildPipelineService pipelineService, IFileSystem fileSystem, ILogger logger) {
            this.localArtifactFiles = localArtifactFiles;
            this.pipelineService = pipelineService;
            this.fileSystem = fileSystem;
            this.logger = logger;
        }

        public string CommonOutputDirectory { get; set; }

        public string CommonDependencyDirectory { get; set; }

        public IReadOnlyCollection<string> StagingDirectoryWhitelist { get; set; }

        /// <summary>
        /// Returns a possible path to a file in the build cache. Relative to the build cache package directory root.
        /// Also returns the new location to place the file if the write location does not match the output path.
        ///
        /// bin\foo.zip => Foo.zip
        /// bin\abc\bar.zip => Abc\Bar.zip
        /// ..\..\bin\module\baz.zip => Baz.zip
        /// </summary>
        /// <returns></returns>
        internal static string CalculateRestorePath(string fileWrite, ref string projectOutputPath, string directoryOfProject = null, string solutionRoot = null) {
            string write = fileWrite;

            int index = write.IndexOf(projectOutputPath, StringComparison.OrdinalIgnoreCase);
            if (index == 0) {
                // Retain the relative path of the build artifact.
                return PathUtility.TrimLeadingSlashes(write.Remove(index, projectOutputPath.Length));
            }

            if (directoryOfProject != null && solutionRoot != null) {
                // FileWrite was not for the output path.
                // If the file write is for a location within our directory boundary then allow it.
                write = Path.GetFullPath(Path.Combine(directoryOfProject, write));

                if (write.StartsWith(solutionRoot, StringComparison.OrdinalIgnoreCase)) {
                    projectOutputPath = Path.GetDirectoryName(fileWrite);
                    projectOutputPath = Path.GetFullPath(Path.Combine(directoryOfProject, projectOutputPath));
                    return CalculateRestorePath(write, ref projectOutputPath, null, null);
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

                if (Path.IsPathRooted(outputPath)) {
                    logger.Warning($"Skipped restoring artifacts for project: '{project.Key}' as output path: '{outputPath}' is rooted.");
                    continue;
                }

                string originalOutputPath = outputPath;

                bool isTestProject = project.Value.IsTestProject;
                ISet<string> targetDirectories = CreateDestinationDirectoryList(directoryOfProject, originalOutputPath, isTestProject);

                if (fileSystem.FileExists(localProjectFile)) {
                    logger.Info($"Calculating files to restore for project: {Path.GetFileNameWithoutExtension(project.Key)}");

                    foreach (string fileWrite in project.Value.FilesWritten) {
                        HashSet<string> actualTargetDirectories = new HashSet<string>(targetDirectories, StringComparer.OrdinalIgnoreCase);

                        // Must reset the path for each iteration as it can be changed by the CalculateRestorePath function.
                        outputPath = originalOutputPath;

                        string filePath = CalculateRestorePath(fileWrite, ref outputPath, directoryOfProject, solutionRoot);

                        // A new project relative output path was calculated, add this allowed path to the path collector.
                        if (!string.Equals(originalOutputPath, outputPath)) {
                            if (actualTargetDirectories.Add(outputPath)) {
                                logger.Info($"File write '{filePath}' outside of project output path was allowed: => {outputPath}");
                            }
                        }

                        // Use relative path for comparison.
                        string fileToFind = string.Concat(@"\", filePath);
                        List<LocalArtifactFile> localSourceFiles = localArtifactFiles.Where(s => s.FullPath.EndsWith(fileToFind, StringComparison.OrdinalIgnoreCase)).ToList();

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

                        CheckWhitelistForFile(filePath, actualTargetDirectories);

                        int copyCount = copyOperations.Count;
                        foreach (string targetDirectory in actualTargetDirectories) {
                            string destination = Path.GetFullPath(Path.Combine(targetDirectory, filePath));

                            AddFileDestination(
                                seenDestinationPaths,
                                artifactFile,
                                destination,
                                copyOperations);
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
        private void CheckWhitelistForFile(string filePath, ISet<string> targetDirectories) {
            if (StagingDirectoryWhitelist != null && !string.IsNullOrWhiteSpace(CommonDependencyDirectory)) {
                foreach (var item in StagingDirectoryWhitelist) {
                    if (WildcardPattern.ContainsWildcardCharacters(item)) {
                        WildcardPattern pattern = WildcardPattern.Get(item, WildcardOptions.IgnoreCase);

                        if (pattern.IsMatch(filePath)) {
                            IncludeFile(filePath, targetDirectories, pattern.ToString());
                            break;
                        }
                    }

                    if (string.Equals(item, filePath, StringComparison.OrdinalIgnoreCase)) {
                        IncludeFile(filePath, targetDirectories, item);
                        break;
                    }
                }
            }
        }

        private void IncludeFile(string filePath, ISet<string> targetDirectories, string pattern) {
            logger.Debug("File '{0}' was included because of: {1}", filePath, pattern);
            targetDirectories.Add(CommonDependencyDirectory);
        }

        private HashSet<string> CreateDestinationDirectoryList(string directoryOfProject, string outputPath, bool isTestProject) {
            // The destination folder paths
            var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
        /// <param name="destination">The destination for <paramref name="file" /></param>
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
    }
}
