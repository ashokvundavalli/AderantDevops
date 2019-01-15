using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;
using Aderant.Build.VersionControl.Model;

namespace Aderant.Build.ProjectSystem {
    internal class BuildDependenciesCollector {
        private SortedList<string, IReference> resolvedReferences = new SortedList<string, IReference>(StringComparer.OrdinalIgnoreCase);
        private SortedList<string, IUnresolvedReference> unresolvedReferences = new SortedList<string, IUnresolvedReference>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<IUnresolvedReference> UnresolvedReferences {
            get {
                lock (this) {
                    return unresolvedReferences.Values.ToList();
                }
            }
        }

        /// <summary>
        /// Gets or sets the project configuration to collect.
        /// </summary>
        public ConfigurationToBuild ProjectConfiguration { get; set; }

        public IReadOnlyCollection<ISourceChange> SourceChanges { get; set; }
        public ExtensibilityImposition ExtensibilityImposition { get; set; }

        public void AddUnresolvedReferences(IReadOnlyCollection<IUnresolvedReference> references) {
            ErrorUtilities.IsNotNull(references, nameof(references));

            foreach (var unresolvedReference in references) {
                lock (this) {
                    unresolvedReferences.Add(unresolvedReference.Id, unresolvedReference);
                }
            }
        }

        public void AddResolvedDependency(IUnresolvedReference existingUnresolvedItem, IReference dependency) {
            ErrorUtilities.IsNotNull(existingUnresolvedItem, nameof(existingUnresolvedItem));
            ErrorUtilities.IsNotNull(dependency, nameof(dependency));

            lock (this) {
                resolvedReferences.Add(dependency.Id, dependency);
                unresolvedReferences.Remove(existingUnresolvedItem.Id);
            }
        }
    }
}
