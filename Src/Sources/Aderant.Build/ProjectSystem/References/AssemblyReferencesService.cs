using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {
    [Export(typeof(IAssemblyReferencesService))]
    [ExportMetadata("Scope", nameof(ProjectSystem.ConfiguredProject))]
    internal class AssemblyReferencesService : AssemblyReferencesServiceBase {

        private static readonly string[] outputTypeValues = {
            "Library",
            "winexe",
            "exe",
        };

        protected override IAssemblyReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, IUnresolvedAssemblyReference unresolved) {
            IReadOnlyCollection<ConfiguredProject> projects = ConfiguredProject.Tree.LoadedConfiguredProjects;

            foreach (var project in projects) {
                var reference = project.Services.AssemblyReferences.SynthesizeResolvedReferenceForProjectOutput(unresolved);

                if (reference != null) {
                    if (string.Equals(unresolved.GetAssemblyName(), reference.ResolvedReference.Id, StringComparison.OrdinalIgnoreCase)) {
                        return (IAssemblyReference)reference.ResolvedReference;
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

        public override ResolvedReference SynthesizeResolvedReferenceForProjectOutput(IUnresolvedAssemblyReference unresolved) {
            var configuredProjectOutputType = ConfiguredProject.OutputType;

            if (outputTypeValues.Contains(configuredProjectOutputType, StringComparer.OrdinalIgnoreCase)) {
                var resolved = new ResolvedReference(ConfiguredProject, unresolved, ConfiguredProject);
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
