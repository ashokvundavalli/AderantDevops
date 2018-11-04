﻿using System;
using System.Diagnostics;

namespace Aderant.Build.ProjectSystem.References {
    [DebuggerDisplay("UnresolvedBuildDependencyProjectReference: {Id}")]
    internal class UnresolvedBuildDependencyProjectReference : UnresolvedReferenceBase<IUnresolvedBuildDependencyProjectReference, IBuildDependencyProjectReference, UnresolvedP2PReferenceMoniker>, IUnresolvedBuildDependencyProjectReference {        

        public UnresolvedBuildDependencyProjectReference(BuildDependencyProjectReferencesService service, UnresolvedP2PReferenceMoniker moniker)
            : base(service, moniker) {
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

        public override string Id {
            get { return ProjectGuid.ToString(); }
        }

        public string GetAssemblyName() {
            return null;
        }
    }
}
