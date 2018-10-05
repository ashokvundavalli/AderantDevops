using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// 
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
        /// Execute the task to generate symlinks for the packages folder
        /// </summary>
        public override bool ExecuteTask() {
            if (File.Exists(Path.Combine(SolutionRoot, LinkLockFileName))) {
                Log.LogMessage("Skipping Symlink creation as {0} is present", LinkLockFileName);
                return true;
            }

            List<DependantFile> dependantFiles = new List<DependantFile>();
            
            string packagesRoot = Path.Combine(SolutionRoot, PackagesDirectoryName);

            List<string> libFolders = Directory.GetDirectories(packagesRoot, $"*{LibDirectoryName}*", SearchOption.AllDirectories).Where(lf => IsLibFolderCorrectDepth(lf, packagesRoot)).ToList();

            foreach (string libFolder in libFolders) {
                dependantFiles.AddRange(
                    from file in Directory.GetFiles(libFolder, "*.*", SearchOption.AllDirectories)
                    let fileName = Path.GetFileName(file)
                    where fileName != null
                    select new DependantFile {
                        FileName = fileName,
                        FileInstance = file
                    });
            }
            
            List<ProjectOutputSnapshot> projectOutputSnapshots = PipelineService.GetAllProjectOutputs().ToList();
            
            foreach (DependantFile df in dependantFiles) {
                var snapShotForDependantFile = projectOutputSnapshots.FirstOrDefault(pos => !pos.IsTestProject && !string.IsNullOrEmpty(pos.AbsoluteProjectFile) && pos.FilesWritten.Any(fw => df.FileName == Path.GetFileName(fw)));
                if (snapShotForDependantFile != null) {

                    string moduleDirectory = Path.GetDirectoryName(snapShotForDependantFile.AbsoluteProjectFile);

                    if (string.IsNullOrEmpty(moduleDirectory)) {
                        continue;
                    }

                    string locationForSymlink = df.FileInstance;
                    string symlinkTargetRawPath = Path.Combine(moduleDirectory, snapShotForDependantFile.OutputPath, df.FileName);
                    // Normalise the path, as symlinks with \..\ in them dont resolve correctly
                    string targetForSymlink = new Uri(symlinkTargetRawPath).LocalPath;
                    
                    if (File.Exists(locationForSymlink)) {
                        File.Delete(locationForSymlink);
                    }

                    NativeMethods.CreateSymbolicLink(locationForSymlink, targetForSymlink, (uint)NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_FILE);
                }
            }

            File.Create(Path.Combine(SolutionRoot, LinkLockFileName));

            return true;
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
