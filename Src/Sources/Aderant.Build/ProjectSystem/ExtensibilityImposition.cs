using System.Collections.Generic;

namespace Aderant.Build.ProjectSystem {
    internal class ExtensibilityImposition {

        public ExtensibilityImposition(IReadOnlyCollection<string> alwaysBuildProjects) {
            AlwaysBuildProjects = alwaysBuildProjects;
        }

        /// <summary>
        /// Projects that should always be built.
        /// The reasons for this are varied, perhaps they have complicated item baggage requirements
        /// which it easier to to regenerate rather than try and replay.
        /// </summary>
        public IReadOnlyCollection<string> AlwaysBuildProjects { get; }

        public Dictionary<string, string> AliasMap { get; set; }

        public bool AlwaysBuildWebProjects { get; set; }

        public bool RequireSynchronizedOutputPathsByConfiguration { get; set; }
    }
}