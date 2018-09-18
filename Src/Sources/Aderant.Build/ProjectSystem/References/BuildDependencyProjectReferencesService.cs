using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Aderant.Build.PipelineService;
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

            try {
                ConfiguredProject dependency = projects.SingleOrDefault(project => project.ProjectGuid == unresolved.ProjectGuid);
                if (dependency != null) {
                    return dependency;
                }
            } catch (InvalidOperationException) {
                IEnumerable<ConfiguredProject> configuredProjects = projects.Where(s => s.ProjectGuid == unresolved.ProjectGuid);
                string paths = string.Join(", ", configuredProjects.Select(s => s.FullPath));

                throw new BuildPlatformException($"The build tree contains more than one project with the same project GUID. Create a new GUID for one of the projects and update all references. The guid was '{unresolved.ProjectGuid}' which clashes with {paths}");
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
