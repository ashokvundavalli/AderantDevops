using System;
using System.Diagnostics;
using System.IO;

namespace Aderant.Build.ProjectSystem.References {
    [DebuggerDisplay("UnresolvedBuildDependencyProjectReference: {Id}")]
    internal class UnresolvedBuildDependencyProjectReference : UnresolvedReferenceBase<IUnresolvedBuildDependencyProjectReference, IBuildDependencyProjectReference, UnresolvedP2PReferenceMoniker>, IUnresolvedBuildDependencyProjectReference {
        public bool OwningProjectIsSdkStyeProject { get; }

        private string fileName;

        public UnresolvedBuildDependencyProjectReference(BuildDependencyProjectReferencesService service, UnresolvedP2PReferenceMoniker moniker, bool owningProjectIsSdkStyeProject)
            : base(service, moniker) {
            OwningProjectIsSdkStyeProject = owningProjectIsSdkStyeProject;
        }

        public Guid ProjectGuid {
            get {
                if (Moniker != null) {
                    return Moniker.ProjectGuid;
                }

                return Guid.Empty;
            }
        }

        public string ProjectPath {
            get { return Moniker.ProjectPath; }
        }

        /// <summary>
        /// The file name portion of <see cref="ProjectPath"/>.
        /// Computed lazily.
        /// </summary>
        public string ProjectFileName {
            get { return fileName ?? (fileName = Path.GetFileName(ProjectPath)); }
        }

        public override string Id {
            get { return ProjectGuid.ToString(); }
        }

        public string GetAssemblyName() {
            return null;
        }
    }
}
