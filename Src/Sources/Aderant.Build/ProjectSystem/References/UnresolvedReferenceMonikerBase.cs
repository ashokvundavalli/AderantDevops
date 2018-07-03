namespace Aderant.Build.ProjectSystem.References {
    internal abstract class UnresolvedReferenceMonikerBase<TUnresolvedReference, TResolvedReference>
        where TUnresolvedReference : class, TResolvedReference, IUnresolvedReference
        where TResolvedReference : class, IReference {
    }
}
