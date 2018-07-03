using System.ComponentModel.Composition;
using System.Reflection;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {

    internal abstract class AssemblyReferencesServiceBase : ResolvableReferencesProviderBase<IUnresolvedAssemblyReference, IAssemblyReference>, IAssemblyReferencesService {
        protected AssemblyReferencesServiceBase()
            : base("Reference") {
        }
    }

    internal class UnresolvedAssemblyReferenceMoniker : UnresolvedReferenceMonikerBase<IUnresolvedAssemblyReference, IAssemblyReference> {
        internal UnresolvedAssemblyReferenceMoniker(AssemblyName assemblyName, string assemblyPath) {
            AssemblyName = assemblyName;
            AssemblyPath = assemblyPath;
        }

        internal AssemblyName AssemblyName { get; private set; }

        internal string AssemblyPath { get; private set; }

        public override string ToString() {
            if (!string.IsNullOrEmpty(AssemblyPath)) {
                return AssemblyPath;
            }

            return AssemblyName.FullName;
        }

        public static UnresolvedAssemblyReferenceMoniker Create(ProjectItem unresolved) {
            string evaluatedInclude = unresolved.EvaluatedInclude;

            AssemblyName assemblyName = new AssemblyName(evaluatedInclude);
            string metadataValue = unresolved.GetMetadataValue("HintPath");

            return new UnresolvedAssemblyReferenceMoniker(assemblyName, metadataValue);
        }
    }

    [Export(typeof(IAssemblyReferencesService))]
    [ExportMetadata("Scope", nameof(ProjectSystem.ConfiguredProject))]
    internal class AssemblyReferencesService : AssemblyReferencesServiceBase {

        protected override IUnresolvedAssemblyReference CreateUnresolvedReference(ProjectItem unresolved) {
            var moniker = UnresolvedAssemblyReferenceMoniker.Create(unresolved);

            var reference = new UnresolvedAssemblyReference(this);
            reference.Initialize(moniker);

            return reference;
        }

        protected IUnresolvedAssemblyReference CreateUnresolvedReference(UnresolvedAssemblyReferenceMoniker moniker) {
            var reference = new UnresolvedAssemblyReference(this);
            reference.Initialize(moniker);
            return reference;
        }
    }
}
