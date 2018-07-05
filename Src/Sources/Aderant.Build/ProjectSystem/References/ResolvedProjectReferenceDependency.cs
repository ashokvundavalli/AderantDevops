using System;
using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {
    internal class ResolvedProjectReferenceDependency : ResolvedDependency<IUnresolvedBuildDependencyProjectReference, ConfiguredProject>, IBuildDependencyProjectReference {
        public ResolvedProjectReferenceDependency(IArtifact configuredProject, IUnresolvedBuildDependencyProjectReference unresolved, ConfiguredProject dependency)
            : base(configuredProject, dependency, unresolved) {
            base.ExistingUnresolvedItem = unresolved;
            base.ResolvedReference = dependency;
        }

        public Guid ProjectGuid {
            get { return ResolvedReference.ProjectGuid; }
        }

        public string Id {
            get { return ResolvedReference.Id; }
        }

        public string GetAssemblyName() {
            return ResolvedReference.OutputAssembly;
        }
    }
}
