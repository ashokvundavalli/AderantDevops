using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {

    internal abstract class AssemblyReferencesServiceBase : ResolvableReferencesProviderBase<IUnresolvedAssemblyReference, IAssemblyReference>, IAssemblyReferencesService {
        protected AssemblyReferencesServiceBase()
            : base("Reference") {
        }

        public abstract IAssemblyReference SynthesizeResolvedReferenceForProjectOutput(IUnresolvedAssemblyReference unresolved);
    }

}
