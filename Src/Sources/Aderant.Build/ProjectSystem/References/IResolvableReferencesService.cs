using System.Collections.Generic;
using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {
    /// <summary>
    /// A component that knows how to read, write, and resolve project references
    /// </summary>
    public interface IResolvableReferencesService<TUnresolvedReference, TResolvedReference>
        where TUnresolvedReference : class, TResolvedReference, IUnresolvedReference
        where TResolvedReference : class, IReference {
        IReadOnlyCollection<TUnresolvedReference> GetUnresolvedReferences();

        IReadOnlyCollection<ResolvedDependency<TUnresolvedReference, TResolvedReference>> GetResolvedReferences(IReadOnlyCollection<IUnresolvedReference> references);
    }

}
