using System;
using System.IO;
using System.Linq;

namespace Aderant.Build.Tasks {
    public class CopyCustomSolution : BuildOperationContextTask {
        public string[] CustomProjects { get; set; }

        public string StagingPackageDrop { get; set; }

        public string CustomSolutionZipPath { get; set; }

        public override bool ExecuteTask() {            
            try {
                var changedProjects = PipelineService.GetImpactedProjects().Intersect(CustomProjects).ToList();
                if (changedProjects.Any()) {
                    Log.LogMessage("Projects with changes affecting custom solution:" + Environment.NewLine + string.Join(Environment.NewLine, changedProjects));
                    var fileName = Path.GetFileName(CustomSolutionZipPath);

                    var StagingDirectoryPackagesFolder = Path.Combine(StagingPackageDrop, "Customization");
                    if (!Directory.Exists(StagingDirectoryPackagesFolder)) {
                        Directory.CreateDirectory(StagingDirectoryPackagesFolder);
                    }

                    var stagingFileName = Path.Combine(StagingDirectoryPackagesFolder, fileName);
                    if (File.Exists(stagingFileName)) {
                        File.Delete(stagingFileName);
                    }
                    Log.LogMessage($"Copying custom solution {fileName} to {stagingFileName}");
                    File.Copy(CustomSolutionZipPath, stagingFileName);
                }

                return !Log.HasLoggedErrors;
            } catch (Exception ex) {
                Log.LogErrorFromException(ex);
                return false;
            }
        }
    }
}
