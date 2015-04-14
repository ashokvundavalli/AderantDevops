using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.MSBuild;
using Aderant.Build.Process;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    public sealed class ParallelBuildProject : Task {

        [Required]
        public ITaskItem[] ModulesInBuild { get; set; }

        public string ModulesDirectory { get; set; }
        
        public string ProductManifest { get; set; }

        public override bool Execute() {
            ParallelBuildProjectController controller = new ParallelBuildProjectController();

            ExpertManifest manifest = new ExpertManifest(FileSystem.Default, XDocument.Load(ProductManifest, LoadOptions.SetBaseUri));
            Project project = controller.CreateProject(ModulesDirectory, manifest, ModulesInBuild.Select(m => Path.GetFileName(m.ItemSpec)));
            XElement projectDocument = controller.CreateProjectDocument(project);

            ParallelBuildProjectController.SaveBuildProject(ModulesDirectory, projectDocument);

            return !Log.HasLoggedErrors;
        }
    }
}