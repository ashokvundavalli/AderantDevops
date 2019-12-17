using System;
using System.Collections.Generic;

namespace Aderant.Build.Packaging {
    internal interface IArtifactHandler {
        BuildArtifact ProcessFiles(IList<PathSpec> copyList, BuildOperationContext context, string artifactId, IReadOnlyCollection<PathSpec> files);
    }
}
