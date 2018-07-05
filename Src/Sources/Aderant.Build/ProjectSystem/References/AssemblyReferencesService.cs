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
                    return reference;
                }
            }

            return null;
        }

        protected override IUnresolvedAssemblyReference CreateUnresolvedReference(ProjectItem unresolved) {
            var moniker = UnresolvedAssemblyReferenceMoniker.Create(unresolved);

            var reference = new UnresolvedAssemblyReference(this, moniker);
            return reference;
        }

        public override IAssemblyReference SynthesizeResolvedReferenceForProjectOutput(IUnresolvedAssemblyReference unresolved) {
            var outputType = ConfiguredProject.OutputType;

            // Project outputs a supported .NET assembly
            if (outputTypeValues.Contains(outputType, StringComparer.OrdinalIgnoreCase)) {
                string assemblyName = unresolved.GetAssemblyName();
                string projectAssemblyName = ConfiguredProject.GetAssemblyName();

                // File extensions do not make up part of the assembly identity
                if (string.Equals(assemblyName, projectAssemblyName, StringComparison.OrdinalIgnoreCase)) {
                    return ConfiguredProject;
                }
            }

            return null;
        }

        protected IUnresolvedAssemblyReference CreateUnresolvedReference(UnresolvedAssemblyReferenceMoniker moniker) {
            var reference = new UnresolvedAssemblyReference(this, moniker);
            return reference;
        }
    }
}
