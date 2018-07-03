using System.Diagnostics;

namespace Aderant.Build.ProjectSystem.References {
    [DebuggerDisplay("UnresolvedBuildDependencyProjectReference: {FullPath}")]
    internal class UnresolvedBuildDependencyProjectReference :
        UnresolvedReferenceBase<IUnresolvedBuildDependencyProjectReference, IBuildDependencyProjectReference>,
        IUnresolvedBuildDependencyProjectReference {
        private UnresolvedP2PReferenceMoniker moniker;

        public UnresolvedBuildDependencyProjectReference(BuildDependencyProjectReferencesService provider)
            : base(provider) {
        }

        public void Initialize(UnresolvedP2PReferenceMoniker moniker) {
            this.moniker = moniker;
        }
    }
}
