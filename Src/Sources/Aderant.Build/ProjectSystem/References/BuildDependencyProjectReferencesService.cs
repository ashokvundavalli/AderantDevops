using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {

    [Export(typeof(IBuildDependencyProjectReferencesService))]
    [ExportMetadata("Scope", nameof(ProjectSystem.ConfiguredProject))]
    internal class BuildDependencyProjectReferencesService : ResolvableReferencesProviderBase<IUnresolvedBuildDependencyProjectReference, IBuildDependencyProjectReference>, IBuildDependencyProjectReferencesService {
        private readonly ILogger logger;

        static List<Guid> projectReferenceGuidsInError = new List<Guid>();

        public static void ClearProjectReferenceGuidsInError() {
            projectReferenceGuidsInError = new List<Guid>();
        }

        [ImportingConstructor]
        public BuildDependencyProjectReferencesService(ILogger logger)
            : base("ProjectReference") {
            this.logger = logger;
        }

        protected override IBuildDependencyProjectReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, IUnresolvedBuildDependencyProjectReference unresolved) {
            var projects = this.ConfiguredProject.Tree.LoadedConfiguredProjects;

            try {
                ConfiguredProject dependency = projects.SingleOrDefault(project => project.ProjectGuid == unresolved.ProjectGuid);
                if (dependency != null) {
                    return dependency;
                }

                var resolvedReference = LocateReferenceByPath(unresolved, projects);
                return resolvedReference;
            } catch (InvalidOperationException) {
                IEnumerable<ConfiguredProject> configuredProjects = projects.Where(s => s.ProjectGuid == unresolved.ProjectGuid);
                string paths = string.Join(", ", configuredProjects.Select(s => s.FullPath));

                throw new BuildPlatformException($"The build tree contains more than one project with the same project GUID. Create a new GUID for one of the projects and update all references. The guid was '{unresolved.ProjectGuid}' which clashes with {paths}");
            }
        }

        private ConfiguredProject LocateReferenceByPath(IUnresolvedBuildDependencyProjectReference unresolved, IReadOnlyCollection<ConfiguredProject> projects) {
            string relativePathToOtherProject = unresolved.ProjectPath;
            string directoryOfThisProject = Path.GetDirectoryName(this.ConfiguredProject.FullPath);
            string fullPathToOtherProject = Path.GetFullPath(Path.Combine(directoryOfThisProject, relativePathToOtherProject));

            var resolvedReference = projects.SingleOrDefault(s => string.Equals(s.FullPath, fullPathToOtherProject, StringComparison.OrdinalIgnoreCase));

            if (resolvedReference != null) {
                if (!projectReferenceGuidsInError.Contains(unresolved.ProjectGuid)) {
                    logger.Warning($"The ProjectReference GUID {unresolved.ProjectGuid} for {unresolved.ProjectPath} does not match the target project of {resolvedReference.ProjectGuid}. Update the Project element to use the correct GUID to prevent the build from selecting the wrong project.");
                    projectReferenceGuidsInError.Add(unresolved.ProjectGuid);
                }
            }

            return resolvedReference;
        }

        protected override IUnresolvedBuildDependencyProjectReference CreateUnresolvedReference(ProjectItem projectItem) {
            var moniker = UnresolvedP2PReferenceMoniker.Create(projectItem);

            var reference = new UnresolvedBuildDependencyProjectReference(this, moniker);
            return reference;
        }
    }
}
