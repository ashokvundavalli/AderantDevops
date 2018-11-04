using System.Xml.Linq;

namespace Aderant.Build.DependencyAnalyzer {
    internal interface IGlobalAttributesProvider {
        XElement MergeAttributes(XElement element);

        void IsReplicationEnabled(DependencyManifest dependencyManifest);
    }
}
