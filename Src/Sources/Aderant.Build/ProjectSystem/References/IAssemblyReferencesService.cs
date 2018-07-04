namespace Aderant.Build.ProjectSystem.References {
    internal interface IAssemblyReferencesService : IResolvableReferencesService<IUnresolvedAssemblyReference, IAssemblyReference> {

        /// <summary>
        /// Synthesizes the resolved reference that represents the output of the project
        /// </summary>
        /// <param name="unresolved"></param>
        ResolvedReference SynthesizeResolvedReferenceForProjectOutput(IUnresolvedAssemblyReference unresolved);
    }
}
