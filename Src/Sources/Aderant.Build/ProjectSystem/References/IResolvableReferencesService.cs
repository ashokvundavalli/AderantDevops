using System.Collections.Generic;
using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {
    /// <summary>
    /// A component that knows how to read, write, and resolve project references
    /// </summary>
    public interface IResolvableReferencesService<TUnresolvedReference, TResolvedReference>
        where TUnresolvedReference : class, IUnresolvedReference
        where TResolvedReference : class, IReference {
        IReadOnlyCollection<TUnresolvedReference> GetUnresolvedReferences();

        /// <summary>
        /// Returns the set references resolved from the set provided.
        /// </summary>
        IReadOnlyCollection<ResolvedDependency<TUnresolvedReference, TResolvedReference>> GetResolvedReferences(IReadOnlyCollection<IUnresolvedReference> references, Dictionary<string, string> aliasMap);
    }

}
