﻿using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.ProjectSystem {
    internal class BuildDependenciesCollector {
        private List<IReference> resolvedReferences = new List<IReference>();
        private List<IUnresolvedReference> unresolvedReferences = new List<IUnresolvedReference>();

        public IReadOnlyCollection<IUnresolvedReference> UnresolvedReferences {
            get {
                lock (this) {
                    return unresolvedReferences.ToList();
                }
            }
        }

        /// <summary>
        /// Gets or sets the project configuration to collect.
        /// </summary>
        public string ProjectConfiguration { get; set; } = "Debug|Any CPU";

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
                //unresolvedReferences.Remove(existingUnresolvedItem);
            }
        }
    }
}
