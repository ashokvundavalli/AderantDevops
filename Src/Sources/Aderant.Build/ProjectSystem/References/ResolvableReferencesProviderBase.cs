using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Aderant.Build.Model;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {

    internal abstract class ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> : IResolvableReferencesService<TUnresolvedReference, TResolvedReference>
        where TUnresolvedReference : class, IUnresolvedReference, TResolvedReference where TResolvedReference : class, IReference {

        private readonly string unresolvedReferenceType;

        protected ResolvableReferencesProviderBase(string unresolvedReferenceType) {
            this.unresolvedReferenceType = unresolvedReferenceType;
        }

        /// <summary>
        /// Gets the configured project this service is bound to.
        /// </summary>
        [Import]
        protected internal ConfiguredProject ConfiguredProject { get; internal set; }

        public List<TUnresolvedReference> UnresolvedReferences { get; set; }

        public virtual IReadOnlyCollection<TUnresolvedReference> GetUnresolvedReferences() {
            ICollection<ProjectItem> projectItems = ConfiguredProject.GetItems(unresolvedReferenceType);

            List<TUnresolvedReference> references = new List<TUnresolvedReference>();

            foreach (var projectItem in projectItems) {
                // TargetFramework >= netstandard2.0 brings any many direct assembly references, ignore these by filtering the extension
                if (!projectItem.EvaluatedInclude.EndsWith(AssemblyReferencesService.DllExtension, StringComparison.OrdinalIgnoreCase)) {
                    var unresolvedReference = CreateUnresolvedReference(projectItem);
                    references.Add(unresolvedReference);
                }
            }

            return UnresolvedReferences = references;
        }

        public IReadOnlyCollection<ResolvedDependency<TUnresolvedReference, TResolvedReference>> GetResolvedReferences(IReadOnlyCollection<IUnresolvedReference> references, Dictionary<string, string> aliasMap) {
            if (UnresolvedReferences == null) {
                throw new InvalidOperationException("GetUnresolvedReferences not called");
            }

            var nowResolvedReferences = new List<ResolvedDependency<TUnresolvedReference, TResolvedReference>>();

            Resolve(UnresolvedReferences, references, nowResolvedReferences, aliasMap);

            foreach (var nowResolvedReference in nowResolvedReferences) {
                UnresolvedReferences.Remove(nowResolvedReference.ExistingUnresolvedItem);
            }

            return nowResolvedReferences;
        }

        private void Resolve(IEnumerable<TUnresolvedReference> list, IReadOnlyCollection<IUnresolvedReference> references, List<ResolvedDependency<TUnresolvedReference, TResolvedReference>> nowResolvedReferences, Dictionary<string, string> aliasMap) {
            foreach (var unresolved in list) {
                TResolvedReference resolvedReference = CreateResolvedReference(references, unresolved, aliasMap);
                if (resolvedReference != null) {
                    nowResolvedReferences.Add(new ResolvedDependency<TUnresolvedReference, TResolvedReference>(ConfiguredProject, resolvedReference, unresolved));
                }
            }
        }

        /// <summary>
        /// Create a resolved reference for an unresolved reference.
        /// </summary>
        /// <param name="references">The set of resolved references.</param>
        /// <param name="unresolved">The reference to resolve.</param>
        /// <param name="aliasMap">A set of assembly aliases. Primary used to map custom T4 directive processors to assemblies</param>
        /// <returns></returns>
        protected abstract TResolvedReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, TUnresolvedReference unresolved, Dictionary<string, string> aliasMap);

        protected abstract TUnresolvedReference CreateUnresolvedReference(ProjectItem unresolved);
    }
}
