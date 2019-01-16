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

            List<Exception> exceptions = new List<Exception>();

            foreach (IUnresolvedReference unresolvedReference in references) {
                lock (this) {
                    if (!unresolvedReferences.ContainsKey(unresolvedReference.Id)) {
                        unresolvedReferences.Add(unresolvedReference.Id, unresolvedReference);
                    } else {
                        IUnresolvedReference reference;
                        unresolvedReferences.TryGetValue(unresolvedReference.Id, out reference);
                        if (reference != null && !reference.Equals(unresolvedReference)) {
                            exceptions.Add(new Exception($"Attempted to add duplicate reference with id: '{unresolvedReference.Id}'. Existing assembly name: '{reference.GetAssemblyName()}', new assembly name: '{unresolvedReference.GetAssemblyName()}'."));
                        }
                    }
                }
            }

            if (exceptions.Any()) {
                throw new AggregateException(exceptions);
            }
        }

        public void AddResolvedDependency(IUnresolvedReference existingUnresolvedItem, IReference dependency) {
            ErrorUtilities.IsNotNull(existingUnresolvedItem, nameof(existingUnresolvedItem));
            ErrorUtilities.IsNotNull(dependency, nameof(dependency));

            lock (this) {
                if (!resolvedReferences.ContainsKey(dependency.Id)) {
                    resolvedReferences.Add(dependency.Id, dependency);
                    unresolvedReferences.Remove(existingUnresolvedItem.Id);
                } else {
                    IReference reference;
                    resolvedReferences.TryGetValue(dependency.Id, out reference);
                    if (reference != null && !reference.Equals(dependency)) {
                        throw new Exception($"Attempted to add duplicate resolved reference with id: '{dependency.Id}'. Existing assembly name: '{reference.GetAssemblyName()}', new assembly name: '{dependency.GetAssemblyName()}'.");
                    }

                    if (unresolvedReferences.ContainsKey(existingUnresolvedItem.Id)) {
                        unresolvedReferences.Remove(existingUnresolvedItem.Id);
                    }
                }
            }
        }
    }
}
