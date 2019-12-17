using System.Xml.Linq;

namespace Aderant.Build.DependencyAnalyzer {
    public class WebModule : ExpertModule {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebModule"/> class.
        /// </summary>
        /// <param name="element">The product manifest module element.</param>
        public WebModule(XElement element) : base(element) {
        }
    }
}
