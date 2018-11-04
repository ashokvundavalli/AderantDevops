namespace Aderant.Build.ProjectSystem.References {
    internal interface IUnresolvedAssemblyReference : IAssemblyReference, IUnresolvedReference {
        string GetHintPath();

        bool IsForTextTemplate { get; }
    }
}
