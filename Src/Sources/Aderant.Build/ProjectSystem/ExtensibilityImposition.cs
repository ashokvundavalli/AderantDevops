using System.Collections.Generic;
using Aderant.Build.ProjectSystem.StateTracking;

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

        /// <summary>
        /// A mapping of aliases.
        /// An alias lets you provide an alternative name for a dependency as the dependency maybe known by a different name in different contexts.
        /// For example if the source tree provides a tool that is used in a custom script then you need to take a dependency on the project that provides that tool
        /// to ensure correct sequencing - but you only know the tool name. This allows the tool author to specify the originating project.
        /// </summary>
        public Dictionary<string, string> AliasMap { get; set; }

        public bool AlwaysBuildWebProjects { get; set; }

        /// <summary>
        /// Requires that for a given platform that all configurations within that platform have the same output path pattern.
        /// </summary>
        public bool RequireSynchronizedOutputPaths { get; set; }

        public bool CreateHardLinksForCopyLocal { get; set; }

        /// <summary>
        /// Controls the behaviour of the build cache.
        /// </summary>
        public BuildCacheOptions BuildCacheOptions { get; set; }
    }
}