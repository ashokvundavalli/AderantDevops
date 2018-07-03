using System.Collections.Generic;

namespace Aderant.Build.ProjectSystem.References {
    /// <summary>
    /// A component that knows how to read, write, and resolve your references
    /// </summary>
    /// <typeparam name="TUnresolvedReference">The type of the t unresolved reference.</typeparam>
    /// <typeparam name="TResolvedReference">The type of the t resolved reference.</typeparam>
    public interface IResolvableReferencesService<out TUnresolvedReference, TResolvedReference>
        where TUnresolvedReference : TResolvedReference
        where TResolvedReference : class {

        IReadOnlyCollection<TUnresolvedReference> GetUnresolvedReferences();

        IReadOnlyCollection<TResolvedReference> GetResolvedReferences(IReadOnlyCollection<IUnresolvedReference> references);
    }
}
