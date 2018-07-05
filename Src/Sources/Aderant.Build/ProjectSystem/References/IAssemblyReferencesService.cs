using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {
    internal interface IAssemblyReferencesService : IResolvableReferencesService<IUnresolvedAssemblyReference, IAssemblyReference> {

        /// <summary>
        /// Synthesizes the resolved reference that represents the output of the project
        /// </summary>
        IAssemblyReference SynthesizeResolvedReferenceForProjectOutput(IUnresolvedAssemblyReference unresolved);
    }
}
