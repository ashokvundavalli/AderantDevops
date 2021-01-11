using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;
using Aderant.Build.VersionControl.Model;

namespace Aderant.Build.ProjectSystem {
    internal class BuildDependenciesCollector {
        private ConcurrentBag<IReference> resolvedReferences = new ConcurrentBag<IReference>();
        private ConcurrentDictionary<IUnresolvedReference, byte> unresolvedReferences = new ConcurrentDictionary<IUnresolvedReference, byte>();

        public IReadOnlyCollection<IUnresolvedReference> UnresolvedReferences {
            get {
                return unresolvedReferences.Keys.ToList();
            }
        }

        /// <summary>
        /// Gets or sets the project configuration to collect.
        /// </summary>
        public ConfigurationToBuild ProjectConfiguration { get; set; }

        public IReadOnlyCollection<ISourceChange> SourceChanges { get; set; }

        public ExtensibilityImposition ExtensibilityImposition { get; set; }

        /// <summary>
        /// File changes that are not associated with a project
        /// </summary>
        public IReadOnlyCollection<ISourceChange> UnreconciledChanges { get; set; } = Array.Empty<ISourceChange>();

        public void AddUnresolvedReferences(IReadOnlyCollection<IUnresolvedReference> references) {
            ErrorUtilities.IsNotNull(references, nameof(references));

            foreach (var unresolvedReference in references) {
                unresolvedReferences.TryAdd(unresolvedReference, 0);
            }
        }

        public void AddResolvedDependency(IUnresolvedReference existingUnresolvedItem, IReference dependency) {
            ErrorUtilities.IsNotNull(existingUnresolvedItem, nameof(existingUnresolvedItem));
            ErrorUtilities.IsNotNull(dependency, nameof(dependency));

            resolvedReferences.Add(dependency);

            byte _;
            unresolvedReferences.TryRemove(existingUnresolvedItem, out _);
        }
    }
}
