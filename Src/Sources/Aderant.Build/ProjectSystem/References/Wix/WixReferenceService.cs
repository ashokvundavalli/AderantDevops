using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References.Wix {
    [Export(typeof(IAssemblyReferencesService))]
    [ExportMetadata("Scope", nameof(ProjectSystem.ConfiguredProject))]
    internal class WixReferenceService : AssemblyReferencesServiceBase {
        protected WixReferenceService()
            : base("WixLibrary") {
        }

        protected override IAssemblyReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, IUnresolvedAssemblyReference unresolved, Dictionary<string, string> aliasMap) {
            if (unresolved is UnresolvedWixReference) {
                IReadOnlyCollection<ConfiguredProject> projects = ConfiguredProject.Tree.LoadedConfiguredProjects;
                string name = unresolved.GetAssemblyName() + WixLibraryExtension;
                return projects.FirstOrDefault(p => string.Equals(p.GetOutputAssemblyWithExtension(), name, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        public static string WixLibraryExtension { get; } = ".wixlib";

        public static string WindowsInstaller { get; } = ".msi";

        public static string PackageType { get; set; } = "package";

        protected override IUnresolvedAssemblyReference CreateUnresolvedReference(ProjectItem unresolved, ConfiguredProject owningProject) {
            var moniker = UnresolvedAssemblyReferenceMoniker.Create(unresolved);

            var reference = new UnresolvedWixReference(this, moniker);
            return reference;
        }

        public override IAssemblyReference SynthesizeResolvedReferenceForProjectOutput(IUnresolvedAssemblyReference unresolved) {
            return null;
        }

        /// <summary>
        /// Returns an file name for a project WiX project output type and assembly name or null;
        /// </summary>
        public static bool TryGetOutputAssemblyWithExtension(string outputType, string outputAssembly, out string name) {
            if (string.Equals(outputType, PackageType, StringComparison.OrdinalIgnoreCase)) {
                name =  outputAssembly + WindowsInstaller;
                return true;
            }

            if (string.Equals(outputType, WindowsInstaller, StringComparison.OrdinalIgnoreCase)) {
                name = outputAssembly + WindowsInstaller;
                return true;
            }

            if (string.Equals(outputType, WixLibraryExtension, StringComparison.OrdinalIgnoreCase)) {
                name = outputAssembly + WixLibraryExtension;
                return true;
            }

            name = null;
            return false;
        }
    }
}
