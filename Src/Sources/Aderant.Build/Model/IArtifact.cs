using System.Collections.Generic;

namespace Aderant.Build.Model {
    public interface IArtifact : IDependable {
        IReadOnlyCollection<IDependable> GetDependencies();
    }
}
