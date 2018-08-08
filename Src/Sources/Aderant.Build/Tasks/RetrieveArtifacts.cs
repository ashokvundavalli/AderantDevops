using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Packaging;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class RetrieveArtifacts : BuildOperationContextTask {

        [Required]
        public string DependencyManifestFile { get; set; }

        [Required]
        public string ArtifactDirectory { get; set; }

        public ITaskItem[] ArtifactDefinitions { get; set; }

        public override bool Execute() {
            if (ArtifactDefinitions != null) {
                var artifacts = ArtifactPackageHelper.MaterializeArtifactPackages(ArtifactDefinitions, null, null);

                var document = XDocument.Load(DependencyManifestFile);
                var manifest = DependencyManifest.Load(document);

                var service = new ArtifactService();
                service.Resolve(Context, manifest, ArtifactDirectory, artifacts);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
