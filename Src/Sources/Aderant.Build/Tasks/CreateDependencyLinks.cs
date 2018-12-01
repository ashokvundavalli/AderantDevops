using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Task to generate symlinks for the packages folder
    /// </summary>
    public class CreateDependencyLinks : BuildOperationContextTask {

        [Required]
        public string SolutionRoot { get; set; }

        private const string PackagesDirectoryName = "packages";
        private const string LibDirectoryName = "lib";
        private const string LinkLockFileName = "link.lock";

        /// <summary>
        /// Execute the task
        /// </summary>
        public override bool ExecuteTask() {
            ErrorUtilities.IsNotNull(SolutionRoot, nameof(SolutionRoot));

            if (File.Exists(Path.Combine(SolutionRoot, LinkLockFileName))) {
                Log.LogMessage("Skipping symlink creation as {0} is present", LinkLockFileName);
                return true;
            }

            string packagesRoot = Path.Combine(SolutionRoot, PackagesDirectoryName);

            if (new DirectoryInfo(packagesRoot).Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                Log.LogMessage("Skipping symlink creation as '{0}' is symlink", LinkLockFileName);
                return true;
            }

            var projectOutputSnapshots = AssignProjectOutputSnapshotsFullPath().ToList();

            List<string> dependencies = new List<string>();

            List<string> libFolders = Directory.GetDirectories(packagesRoot, $"*{LibDirectoryName}*", SearchOption.AllDirectories).Where(lf => IsLibFolderCorrectDepth(lf, packagesRoot)).ToList();

            foreach (string libFolder in libFolders) {
                dependencies.AddRange(Directory.GetFiles(libFolder, "*.*", SearchOption.AllDirectories));
            }

            foreach (var dependencyFile in dependencies) {
                if (dependencyFile.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) {
                    Log.LogMessage("Skipping symlink creation as '{0}' is not important", dependencyFile);
                    continue;
                }

                var snapshots = projectOutputSnapshots.Where(pos => pos.FileNamesWritten.Any(fw => dependencyFile.IndexOf(fw, StringComparison.OrdinalIgnoreCase) >= 0));

                foreach (var snapshot in snapshots) {

                    string locationForSymlink;
                    string targetForSymlink;
                    CalculateSymlinkTarget(snapshot, dependencyFile, out locationForSymlink, out targetForSymlink);

                    if (locationForSymlink != null && targetForSymlink != null) {
                        if (File.Exists(targetForSymlink)) {
                            if (File.Exists(locationForSymlink)) {
                                File.Delete(locationForSymlink);
                            }

                            Log.LogMessage($"Replacing {locationForSymlink} with symlink -> {targetForSymlink}");
                            NativeMethods.CreateSymbolicLink(locationForSymlink, targetForSymlink, (uint)NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_FILE);
                            break;
                        }

                        throw new FileNotFoundException(null, targetForSymlink);
                    }
                }
            }

            File.Create(Path.Combine(SolutionRoot, LinkLockFileName));

            Log.LogMessage("{0} created", LinkLockFileName);

            return !Log.HasLoggedErrors;
        }

        internal void CalculateSymlinkTarget(ProjectOutputSnapshotWithFullPath snapshotForDependentFile, string dependency, out string locationForSymlink, out string targetForSymlink) {
            ErrorUtilities.IsNotNull(snapshotForDependentFile.OutputPath, nameof(snapshotForDependentFile.OutputPath));

            locationForSymlink = null;
            targetForSymlink = null;

            if (string.Equals(SolutionRoot, snapshotForDependentFile.Directory)) {
                // Do not link to ourselves
                // This prevents
                // Foo\packages\ThirdParty.AddinExpress\lib\adxloader.dll -> Foo\Bin\Module\adxloader.dll
                return;
            }

            string moduleDirectory = Path.GetDirectoryName(snapshotForDependentFile.ProjectFileAbsolutePath);

            if (string.IsNullOrEmpty(moduleDirectory)) {
                return;
            }

            string targetPath = null;
            foreach (var file in snapshotForDependentFile.FilesWritten) {
                string candidateFile = file.Substring(snapshotForDependentFile.OutputPath.Length).TrimStart(Path.DirectorySeparatorChar);

                if (dependency.EndsWith(candidateFile, StringComparison.OrdinalIgnoreCase)) {
                    targetPath = file;
                    break;
                }
            }

            if (targetPath == null) {
                // No match, bail
                return;
            }

            locationForSymlink = dependency;
            string symlinkTargetRawPath = Path.Combine(moduleDirectory, targetPath);

            // Normalize the path, as symlinks with \..\ in them don't resolve correctly
            targetForSymlink = Path.GetFullPath(symlinkTargetRawPath);
        }

        private IEnumerable<ProjectOutputSnapshotWithFullPath> AssignProjectOutputSnapshotsFullPath() {
            var trackedProjects = PipelineService
                .GetTrackedProjects()
                .ToDictionary(d => d.ProjectGuid, project => project);

            var rootDirectory = Path.GetFileName(SolutionRoot);
            List<ProjectOutputSnapshot> projectOutputSnapshots = PipelineService
                .GetAllProjectOutputs()
                .Where(s => !string.Equals(s.Directory, rootDirectory, StringComparison.OrdinalIgnoreCase) && !s.IsTestProject)
                .ToList();

            foreach (var item in projectOutputSnapshots) {
                OnDiskProjectInfo onDiskProject;
                if (trackedProjects.TryGetValue(item.ProjectGuid, out onDiskProject)) {

                    if (onDiskProject != null) {
                        var snapshot = new ProjectOutputSnapshotWithFullPath(item) {
                            ProjectFileAbsolutePath = onDiskProject.FullPath
                        };

                        snapshot.BuildFileNamesWritten();

                        yield return snapshot;
                    }
                }
            }
        }

        private static bool IsLibFolderCorrectDepth(string libFolder, string packagesRoot) {
            var relativeFolder = libFolder.Replace(packagesRoot, PackagesDirectoryName);
            var split = relativeFolder.Split('\\').ToList();
            var index = split.IndexOf(LibDirectoryName);
            if (index > 2) {
                return false;
            }

            return true;
        }
    }
}
