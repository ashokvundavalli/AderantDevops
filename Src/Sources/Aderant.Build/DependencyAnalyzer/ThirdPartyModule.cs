using System.IO;
using System.Xml.Linq;

namespace Aderant.Build.DependencyAnalyzer {

    public class ThirdPartyModule : ExpertModule {

        public ThirdPartyModule(XElement element) : base(element) {
            
        }

        protected override string GetBinariesPath(string dropLocationDirectory) {
            return Path.Combine(dropLocationDirectory, Name, "bin");
        }
    }
}