using System;
using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {
    internal interface IBuildDependencyProjectReference : IReference {
        Guid ProjectGuid { get; }
    }
}
