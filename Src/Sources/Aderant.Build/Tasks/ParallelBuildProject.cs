using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.MSBuild;
using Aderant.Build.Process;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class ParallelBuildProjectFactory : Task {

        [Required]
        public ITaskItem[] ModulesInBuild { get; set; }

        public string ModulesDirectory { get; set; }

        public string ProductManifest { get; set; }

        public string ProjectFile { get; set; }

        public override bool Execute() {
            Run();
            return !Log.HasLoggedErrors;
        }

        private void Run([CallerFilePath] string sourceFilePath = "") {
            try {
                ParallelBuildProjectController controller = new ParallelBuildProjectController();

                ExpertManifest manifest = ExpertManifest.Load(ProductManifest);
                manifest.ModulesDirectory = ModulesDirectory;

                Log.LogMessage("Creating build project...");
                Project project = controller.CreateProject(ModulesDirectory, manifest, ModulesInBuild.Select(m => Path.GetFileName(m.ItemSpec)));
                XElement projectDocument = controller.CreateProjectDocument(project);

                ParallelBuildProjectController.SaveBuildProject(Path.Combine(ModulesDirectory, ProjectFile), projectDocument);
            } catch (Exception ex) {
                Log.LogErrorFromException(ex, true, true, sourceFilePath);
                throw;
            }
        }
    }
}