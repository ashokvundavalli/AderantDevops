using System;
using System.Diagnostics;

namespace Aderant.Build.ProjectSystem.References {
    [DebuggerDisplay("UnresolvedBuildDependencyProjectReference: {ProjectGuid} {IsResolved}")]
    internal class UnresolvedBuildDependencyProjectReference : UnresolvedReferenceBase<IUnresolvedBuildDependencyProjectReference, IBuildDependencyProjectReference, UnresolvedP2PReferenceMoniker>, IUnresolvedBuildDependencyProjectReference {        

        public UnresolvedBuildDependencyProjectReference(BuildDependencyProjectReferencesService service, UnresolvedP2PReferenceMoniker moniker)
            : base(service, moniker) {
        }

        public UnresolvedBuildDependencyProjectReference(BuildDependencyProjectReferencesService service, ConfiguredProject project)
            : base(service, project) {
        }

        public Guid ProjectGuid {
            get {
                if (moniker != null) {
                    return moniker.ProjectGuid;
                }

                if (IsResolved) {
                    return Project.ProjectGuid;
                }

                return Guid.Empty;
            }
        }
    }
}
