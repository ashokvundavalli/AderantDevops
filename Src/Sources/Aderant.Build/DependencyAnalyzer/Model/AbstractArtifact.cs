using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.DependencyAnalyzer.Model {
    public abstract class AbstractArtifact : IArtifact {
        private SortedList<string, IResolvedDependency> resolvedDependencies = new SortedList<string, IResolvedDependency>(StringComparer.OrdinalIgnoreCase);
        private SortedList<string, IUnresolvedDependency> unresolvedDependencies = new SortedList<string, IUnresolvedDependency>(StringComparer.OrdinalIgnoreCase);

        public virtual IReadOnlyCollection<IDependable> GetDependencies() {
            return resolvedDependencies.Select(d => d.Value.ResolvedReference).ToList();
        }

        public virtual IResolvedDependency AddResolvedDependency(IUnresolvedDependency unresolvedDependency, IDependable dependable) {
            IResolvedDependency resolvedDependency;

            if (unresolvedDependency == null) {
                resolvedDependency = ResolvedDependency.Create(this, dependable);
            } else {
                resolvedDependency = ResolvedDependency.Create(this, dependable, unresolvedDependency);
            }

            if (!resolvedDependencies.ContainsKey(resolvedDependency.Artifact.Id)) {
                resolvedDependencies.Add(resolvedDependency.Artifact.Id, resolvedDependency);
            }

            if (unresolvedDependency != null && unresolvedDependencies.ContainsKey(unresolvedDependency.Id)) {
                unresolvedDependencies.Remove(unresolvedDependency.Id);
            }

            return resolvedDependency;
        }

        public abstract string Id { get; }
    }
}
