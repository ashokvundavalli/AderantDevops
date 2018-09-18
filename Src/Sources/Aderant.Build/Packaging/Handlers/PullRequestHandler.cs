using System;
using System.Collections.Generic;

namespace Aderant.Build.Packaging.Handlers {
    internal class PullRequestHandler : IArtifactHandler {
        public BuildArtifact ProcessFiles(IList<PathSpec> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files) {
            return null;
        }
    }
}
