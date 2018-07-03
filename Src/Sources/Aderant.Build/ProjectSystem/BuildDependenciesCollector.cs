using System.Collections.Concurrent;
using System.Collections.Generic;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.ProjectSystem {
    internal class BuildDependenciesCollector {
        private ConcurrentBag<IUnresolvedReference> unresolvedReferences = new ConcurrentBag<IUnresolvedReference>();

        public IReadOnlyCollection<IUnresolvedReference> UnresolvedReferences {
            get { return unresolvedReferences; }
        }

        public void AddUnresolvedReferences(IReadOnlyCollection<IUnresolvedReference> references) {
            ErrorUtilities.IsNotNull(references, nameof(references));

            foreach (var unresolvedReference in references) {
                unresolvedReferences.Add(unresolvedReference);
            }
        }
    }
}
