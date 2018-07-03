using System.ComponentModel.Composition;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {

    [Export(typeof(IBuildDependencyProjectReferencesService))]
    [ExportMetadata("Scope", nameof(ProjectSystem.ConfiguredProject))]
    internal class BuildDependencyProjectReferencesService : ResolvableReferencesProviderBase<IUnresolvedBuildDependencyProjectReference, IBuildDependencyProjectReference>, IBuildDependencyProjectReferencesService, IResolvableReferencesService<IUnresolvedBuildDependencyProjectReference, IBuildDependencyProjectReference> {

        public BuildDependencyProjectReferencesService()
            : base("ProjectReference") {
        }

        protected override IUnresolvedBuildDependencyProjectReference CreateUnresolvedReference(ProjectItem projectItem) {
            var moniker = UnresolvedP2PReferenceMoniker.Create(projectItem);

            var reference = new UnresolvedBuildDependencyProjectReference(this);
            reference.Initialize(moniker);
            return reference;
        }
    }
}
