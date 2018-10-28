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

        private class DependantFile {
            public string FileName { get; set; }
            public string FileInstance { get; set; }
        }

        /// <summary>
        /// Execute the task
        /// </summary>
        public override bool ExecuteTask() {
            ErrorUtilities.IsNotNull(SolutionRoot, nameof(SolutionRoot));

            if (File.Exists(Path.Combine(SolutionRoot, LinkLockFileName))) {
                Log.LogMessage("Skipping symlink creation as {0} is present", LinkLockFileName);
                return true;
            }
            
            var projectOutputSnapshots = AssignProjectOutputSnapshotsFullPath().ToList();

            List<DependantFile> dependentFiles = new List<DependantFile>();

            string packagesRoot = Path.Combine(SolutionRoot, PackagesDirectoryName);

            List<string> libFolders = Directory.GetDirectories(packagesRoot, $"*{LibDirectoryName}*", SearchOption.AllDirectories).Where(lf => IsLibFolderCorrectDepth(lf, packagesRoot)).ToList();

            foreach (string libFolder in libFolders) {
                dependentFiles.AddRange(
                    from file in Directory.GetFiles(libFolder, "*.*", SearchOption.AllDirectories)
                    let fileName = Path.GetFileName(file)
                    where fileName != null
                    select new DependantFile {
                        FileName = fileName,
                        FileInstance = file
                    });
            }

            foreach (DependantFile df in dependentFiles) {
                var snapShotForDependentFile = projectOutputSnapshots.FirstOrDefault(pos => pos.FileNamesWritten.Any(fw => string.Equals(df.FileName, fw, StringComparison.OrdinalIgnoreCase)));

                if (snapShotForDependentFile != null) {
                    string moduleDirectory = Path.GetDirectoryName(snapShotForDependentFile.ProjectFileAbsolutePath);

                    if (string.IsNullOrEmpty(moduleDirectory)) {
                        continue;
                    }

                    string locationForSymlink = df.FileInstance;
                    string symlinkTargetRawPath = Path.Combine(moduleDirectory, snapShotForDependentFile.OutputPath, df.FileName);

                    // Normalize the path, as symlinks with \..\ in them dont resolve correctly
                    string targetForSymlink = Path.GetFullPath(symlinkTargetRawPath);
                    
                    if (File.Exists(locationForSymlink)) {
                        File.Delete(locationForSymlink);
                    }

                    NativeMethods.CreateSymbolicLink(locationForSymlink, targetForSymlink, (uint)NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_FILE);
                }
            }

            File.Create(Path.Combine(SolutionRoot, LinkLockFileName));

            Log.LogMessage("{0} created", LinkLockFileName);

            return !Log.HasLoggedErrors;
        }

        private IEnumerable<ProjectOutputSnapshotWithFullPath> AssignProjectOutputSnapshotsFullPath() {
            // TODO: Need to read dependency manifest here to get the list of artifacts, then rebuild that
            // Get all projects analyzed by the build, this gives us all seen projects regardless of their state
            var trackedProjects = PipelineService
                .GetTrackedProjects()
                .ToDictionary(d => d.ProjectGuid, project => project);

            var rootDirectory = Path.GetFileName(SolutionRoot);
            List<ProjectOutputSnapshot> projectOutputSnapshots = PipelineService
                .GetAllProjectOutputs()
                .Where(s => !string.Equals(s.Directory, rootDirectory, StringComparison.OrdinalIgnoreCase) && !s.IsTestProject)
                .ToList();

            foreach (var item in projectOutputSnapshots) {
                TrackedProject trackedProject;
                if (trackedProjects.TryGetValue(item.ProjectGuid, out trackedProject)) {

                    if (trackedProject != null) {
                        var snapshot = new ProjectOutputSnapshotWithFullPath(item) {
                            ProjectFileAbsolutePath = trackedProject.FullPath
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

    internal class SnapshotPair {
        public TrackedProject TrackedProject { get; set; }
        public ProjectOutputSnapshot Snapshot { get; set; }
    }
}
