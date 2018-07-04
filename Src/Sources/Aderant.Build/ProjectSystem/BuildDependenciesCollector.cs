using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.ProjectSystem {
    internal class BuildDependenciesCollector {
        private List<IUnresolvedReference> unresolvedReferences = new List<IUnresolvedReference>();
        private List<IReference> resolvedReferences = new List<IReference>();

        public IReadOnlyCollection<IUnresolvedReference> UnresolvedReferences {
            get {
                lock (this) {
                    return unresolvedReferences.ToList();
                }
            }
        }

        public void AddUnresolvedReferences(IReadOnlyCollection<IUnresolvedReference> references) {
            ErrorUtilities.IsNotNull(references, nameof(references));

            foreach (var unresolvedReference in references) {
                lock (this) {
                    unresolvedReferences.Add(unresolvedReference);
                }
            }
        }

        public void AddResolvedDependency(IUnresolvedReference existingUnresolvedItem, IReference dependency) {
            ErrorUtilities.IsNotNull(existingUnresolvedItem, nameof(existingUnresolvedItem));
            ErrorUtilities.IsNotNull(dependency, nameof(dependency));

            lock (this) {
                resolvedReferences.Add(dependency);
                unresolvedReferences.Remove(existingUnresolvedItem);
            }
        }
    }
}
