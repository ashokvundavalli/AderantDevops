using System.Collections.Generic;

namespace Aderant.Build.Packaging {
    internal class PublishCommands {
        public IEnumerable<PathSpec> ArtifactPaths { get; set; }
        public IEnumerable<string> AssociationCommands { get; set; }
    }
}
