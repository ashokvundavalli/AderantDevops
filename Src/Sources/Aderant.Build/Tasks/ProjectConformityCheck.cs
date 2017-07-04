using System;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class ProjectConformityCheck : Microsoft.Build.Utilities.Task {
        public ITaskItem Project { get; set; }
        public bool IsDesktopBuild { get; set; }

        public override bool Execute() {
            return ExecuteInternal();
        }

        private bool ExecuteInternal([CallerFilePath] string file = null) {
            try {
                var controller = new ProjectConformityController(new PhysicalFileSystem(System.IO.Path.GetDirectoryName(Project.ItemSpec)), ProjectConformityController.CreateProject(Project.ItemSpec));

                //if (!controller.ValidateProjectOutputPaths()) {
                //    if (IsDesktopBuild) {
                //        Log.LogWarning("The project file {0} contains different output paths for Debug and Release builds. These are expected to be matching", Project.ItemSpec);
                //    } else {
                //        Log.LogError("The project file {0} contains different output paths for Debug and Release builds. These are expected to be matching", Project.ItemSpec);
                //    }
                //}

                if (controller.AddDirProjectIfNecessary()) {
                    Log.LogWarning("The project file {0} does not have a dir.proj import. One will be added.", Project.ItemSpec);
                    controller.Save();
                }

                controller.Unload();
            } catch (Exception ex) {
                Log.LogErrorFromException(ex, true, true, file);
            }

            return !Log.HasLoggedErrors;
        }
    }
}