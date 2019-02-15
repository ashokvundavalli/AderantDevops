using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.DependencyAnalyzer.Model {
    
    public abstract class AbstractArtifact : IArtifact {
        private List<IResolvedDependency> resolvedDependencies = new List<IResolvedDependency>();
        private List<IUnresolvedDependency> unresolvedDependencies = new List<IUnresolvedDependency>();

        public virtual IReadOnlyCollection<IDependable> GetDependencies() {
            return resolvedDependencies.Select(d => d.ResolvedReference).ToList();
        }

        public virtual IResolvedDependency AddResolvedDependency(IUnresolvedDependency unresolvedDependency, IDependable dependable) {
            IResolvedDependency resolvedDependency;

            if (FindExistingResolvedDependency(dependable, out resolvedDependency)) {
                return resolvedDependency;
            }

            if (unresolvedDependency == null) {
                resolvedDependency = ResolvedDependency.Create(this, dependable);
            } else {
                resolvedDependency = ResolvedDependency.Create(this, dependable, unresolvedDependency);
            }

            resolvedDependencies.Add(resolvedDependency);

            if (unresolvedDependency != null) {
                unresolvedDependencies.Remove(unresolvedDependency);
            }

            return resolvedDependency;
        }

        public abstract string Id { get; }

        private bool FindExistingResolvedDependency(IDependable dependable, out IResolvedDependency resolvedDependency) {
            // Prevent duplicates - duplicates do not harm the system but we want to reduce confusion in the internal state
            for (var i = 0; i < resolvedDependencies.Count; i++) {
                var dependency = resolvedDependencies[i];
                if (dependency.ResolvedReference == dependable) {

                    resolvedDependency = dependency;
                    return true;

                }
            }

            resolvedDependency = null;
            return false;
        }
    }
}