namespace Aderant.Build.ProjectSystem.References {
    internal interface IUnresolvedAssemblyReference : IAssemblyReference, IReference, IUnresolvedReference {
        string GetHintPath();
    }
}
