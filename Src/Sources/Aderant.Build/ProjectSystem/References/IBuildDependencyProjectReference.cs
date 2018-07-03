using System;

namespace Aderant.Build.ProjectSystem.References {
    internal interface IBuildDependencyProjectReference : IReference {
        Guid ProjectGuid { get; }
    }
}
