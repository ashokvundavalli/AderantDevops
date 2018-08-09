using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class PackageArtifactReader : Task {

        [Output]
        public string[] ArtifactIds { get; set; }

        public string ProjectXml { get; set; }

        public string ProjectFile { get; set; }

        public override bool Execute() {
         
            XDocument document = null;
            if (ProjectFile != null) {
                document = XDocument.Load(ProjectFile);
            } else if (ProjectXml != null) {
                document = XDocument.Parse(ProjectXml);
            }

            ErrorUtilities.IsNotNull(document, nameof(document));

            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            if (document.Root != null) {
                var xElements = document.Root
                    .Descendants(ns + "PackageArtifact")
                    .Descendants(ns + "ArtifactId");

                ArtifactIds = xElements.Select(s => s.Value).ToArray();
            }

            return !Log.HasLoggedErrors;
        }
    }
}
