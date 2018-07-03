using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {

    /// <summary>
    /// An UnresolvedReference is a pair consisting of a symbolic artifact identifier and a version.
    /// UnresolvedReference objects are used to represent dependencies between artifacts when the actual artifacts are not
    /// known yet.
    /// During the analysis process performed by engine, UnresolvedReference objects are replaced by links to actual artifact
    /// objects.
    /// </summary>
    internal abstract class ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> : IResolvableReferencesService<TUnresolvedReference, TResolvedReference>, IDisposable
        where TUnresolvedReference : class, IUnresolvedReference, TResolvedReference where TResolvedReference : class, IReference {

        private readonly string unresolvedReferenceType;
        private List<TUnresolvedReference> unresolvedReferences;

        protected ResolvableReferencesProviderBase(string unresolvedReferenceType) {
            this.unresolvedReferenceType = unresolvedReferenceType;
        }

        /// <summary>
        /// Gets the configured project this service is bound to.
        /// </summary>
        [Import]
        protected internal ConfiguredProject ConfiguredProject { get; private set; }

        public void Dispose() {
            this.Dispose(true);
        }

        public virtual IReadOnlyCollection<TUnresolvedReference> GetUnresolvedReferences() {
            ICollection<ProjectItem> projectItems = ConfiguredProject.GetItems(unresolvedReferenceType);

            List<TUnresolvedReference> references = new List<TUnresolvedReference>();

            foreach (var projectItem in projectItems) {
                var unresolvedReference = CreateUnresolvedReference(projectItem);
                references.Add(unresolvedReference);
            }

            return unresolvedReferences = references;
        }

        public IReadOnlyCollection<TResolvedReference> GetResolvedReferences(IReadOnlyCollection<IUnresolvedReference> references) {
            var resolved = new List<TResolvedReference>();

            List<TUnresolvedReference> nowResolvedReferences = new List<TUnresolvedReference>();

            foreach (var unresolved in unresolvedReferences) {
                TResolvedReference resolvedReference = CreateResolvedReference(references, unresolved);

                if (resolvedReference != null) {
                    nowResolvedReferences.Add(unresolved);
                    resolved.Add(resolvedReference);
                }
            }

            foreach (TUnresolvedReference nowResolvedReference in nowResolvedReferences) {
                unresolvedReferences.Remove(nowResolvedReference);
            }
            
            return resolved;
        }

        protected abstract TResolvedReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, TUnresolvedReference unresolved);

        protected abstract TUnresolvedReference CreateUnresolvedReference(ProjectItem unresolved);

        protected virtual void Dispose(bool disposing) {
        }
    }
}
