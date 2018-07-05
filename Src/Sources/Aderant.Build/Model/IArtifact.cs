using System.Collections.Generic;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.Model {
    public interface IArtifact : IDependable {
        IReadOnlyCollection<IDependable> GetDependencies();

        void AddResolvedDependency(IUnresolvedDependency unresolvedDependency, IDependable completionNode);
    }
}
