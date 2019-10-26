namespace Aderant.Build.ProjectSystem.References {

    internal abstract class AssemblyReferencesServiceBase : ResolvableReferencesProviderBase<IUnresolvedAssemblyReference, IAssemblyReference>, IAssemblyReferencesService {
        protected AssemblyReferencesServiceBase()
            : base("Reference") {
        }

        protected AssemblyReferencesServiceBase(string projectItemReferenceType)
            : base(projectItemReferenceType) {
        }

        public abstract IAssemblyReference SynthesizeResolvedReferenceForProjectOutput(IUnresolvedAssemblyReference unresolved);
    }


}