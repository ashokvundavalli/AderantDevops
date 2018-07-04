namespace Aderant.Build.ProjectSystem.References {

    internal abstract class AssemblyReferencesServiceBase : ResolvableReferencesProviderBase<IUnresolvedAssemblyReference, IAssemblyReference>, IAssemblyReferencesService {
        protected AssemblyReferencesServiceBase()
            : base("Reference") {
        }

        public abstract ResolvedReference SynthesizeResolvedReferenceForProjectOutput(IUnresolvedAssemblyReference unresolved);
    }

}
