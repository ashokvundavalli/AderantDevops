using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {

    [Export(typeof(IBuildDependencyProjectReferencesService))]
    [ExportMetadata("Scope", nameof(ProjectSystem.ConfiguredProject))]
    internal class BuildDependencyProjectReferencesService : ResolvableReferencesProviderBase<IUnresolvedBuildDependencyProjectReference, IBuildDependencyProjectReference>, IBuildDependencyProjectReferencesService {

        public BuildDependencyProjectReferencesService()
            : base("ProjectReference") {
        }

        protected override IBuildDependencyProjectReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, IUnresolvedBuildDependencyProjectReference unresolved) {
            var projects = this.ConfiguredProject.Tree.LoadedConfiguredProjects;

            ConfiguredProject dependency = projects.SingleOrDefault(project => project.ProjectGuid == unresolved.ProjectGuid);

            if (dependency != null) {
                var resolved = new UnresolvedBuildDependencyProjectReference(this, dependency);
                return resolved;
            }

            return null;
        }

        protected override IUnresolvedBuildDependencyProjectReference CreateUnresolvedReference(ProjectItem projectItem) {
            var moniker = UnresolvedP2PReferenceMoniker.Create(projectItem);

            var reference = new UnresolvedBuildDependencyProjectReference(this, moniker);
            return reference;
        }
    }
}
