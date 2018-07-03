using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {
    [Export(typeof(IAssemblyReferencesService))]
    [ExportMetadata("Scope", nameof(ProjectSystem.ConfiguredProject))]
    internal class AssemblyReferencesService : AssemblyReferencesServiceBase {

        private static readonly string[] outputTypeValues = new[] {
            "Library",
            "winexe",
            "exe",
        };

        protected override IAssemblyReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, IUnresolvedAssemblyReference unresolved) {
            IReadOnlyCollection<ConfiguredProject> projects = ConfiguredProject.Tree.LoadedConfiguredProjects;

            foreach (var project in projects) {
                IAssemblyReference reference = project.Services.AssemblyReferences.SynthesizeResolvedReferenceForProjectOutput();

                if (reference != null) {
                    if (unresolved.GetAssemblyName() == reference.GetAssemblyName()) {
                        return reference;
                    }
                }
            }

            return null;
        }

        protected override IUnresolvedAssemblyReference CreateUnresolvedReference(ProjectItem unresolved) {
            var moniker = UnresolvedAssemblyReferenceMoniker.Create(unresolved);

            var reference = new UnresolvedAssemblyReference(this, moniker);
            return reference;
        }

        public override IAssemblyReference SynthesizeResolvedReferenceForProjectOutput() {
            var configuredProjectOutputType = ConfiguredProject.OutputType;

            if (outputTypeValues.Contains(configuredProjectOutputType, StringComparer.OrdinalIgnoreCase)) {
                var resolved = new UnresolvedAssemblyReference(this, ConfiguredProject);
                return resolved;
            }

            return null;
        }

        protected IUnresolvedAssemblyReference CreateUnresolvedReference(UnresolvedAssemblyReferenceMoniker moniker) {
            var reference = new UnresolvedAssemblyReference(this, moniker);
            return reference;
        }
    }
}
