namespace Aderant.Build.ProjectSystem.References {

    internal interface IUnresolvedBuildDependencyProjectReference : IBuildDependencyProjectReference, IUnresolvedReference {

        string ProjectPath { get; }

    }
}
