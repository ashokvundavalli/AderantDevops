using System.Xml.Linq;
using Aderant.Build.DependencyResolver;

namespace Aderant.Build.DependencyAnalyzer {
    public sealed class ThirdPartyModule : ExpertModule {
        public ThirdPartyModule(XElement element)
            : base(element) {
        }
    }
}