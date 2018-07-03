using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {



    /// <summary>
    /// An UnresolvedReference is a pair consisting of a symbolic artifact identifier and a version.
    /// UnresolvedReference objects are used to represent dependencies between artifacts when the actual artifacts are not known yet.
    /// During the analysis process performed by engine, UnresolvedReference objects are replaced by links to actual artifact objects.
    /// </summary>
    internal abstract class ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> : /*ProjectItemProviderBase<TUnresolvedReference>,*/
        IResolvableReferencesService<TUnresolvedReference, TResolvedReference>, IDisposable
        where TUnresolvedReference : class /*, IProjectItem*/, IUnresolvedReference, TResolvedReference where TResolvedReference : class, IReference {

        private readonly string unresolvedReferenceType;

        protected ResolvableReferencesProviderBase(string unresolvedReferenceType) {
            this.unresolvedReferenceType = unresolvedReferenceType;
        }

        [Import]
        protected internal ConfiguredProject ConfiguredProject { get; private set; }

        public void Dispose() {
            this.Dispose(true);
        }

        public virtual IReadOnlyCollection<TUnresolvedReference> GetUnresolvedReferences() {
            ICollection<ProjectItem> projectItems = ConfiguredProject.GetItems(unresolvedReferenceType);

            List<TUnresolvedReference> unresolvedReferences = new List<TUnresolvedReference>();

            foreach (var projectItem in projectItems) {
                var unresolvedReference = CreateUnresolvedReference(projectItem);
                unresolvedReferences.Add(unresolvedReference);
            }

            return unresolvedReferences;
        }

        protected virtual void Dispose(bool disposing) {
        }

        protected abstract TUnresolvedReference CreateUnresolvedReference(ProjectItem unresolved);

        protected struct ReferenceFromProjectResults {
            public TUnresolvedReference ExistingUnresolvedItem { get; private set; }

            public TResolvedReference ResolvedReference { get; private set; }

            public ReferenceFromProjectResults(TUnresolvedReference existingUnresolvedItem, TResolvedReference resolvedReference) {
                this = default(ReferenceFromProjectResults);
                this.ExistingUnresolvedItem = existingUnresolvedItem;
                this.ResolvedReference = resolvedReference;
            }
        }
    }

}
