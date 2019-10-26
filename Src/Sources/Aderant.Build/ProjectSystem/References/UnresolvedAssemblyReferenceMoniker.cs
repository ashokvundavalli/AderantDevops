using System.Reflection;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {
    internal class UnresolvedAssemblyReferenceMoniker {
        internal UnresolvedAssemblyReferenceMoniker(AssemblyName assemblyName, string assemblyPath) {
            AssemblyName = assemblyName;
            AssemblyPath = assemblyPath;
        }

        internal AssemblyName AssemblyName { get; private set; }

        internal string AssemblyPath { get; private set; }

        public bool IsFromTextTemplate { get; set; }

        public override string ToString() {
            if (!string.IsNullOrEmpty(AssemblyPath)) {
                return AssemblyPath;
            }

            return AssemblyName.FullName;
        }

        /// <summary>
        /// Wraps a project item for use in dependency analysis.
        /// </summary>
        public static UnresolvedAssemblyReferenceMoniker Create(ProjectItem unresolved) {
            string evaluatedInclude = unresolved.EvaluatedInclude;

            AssemblyName assemblyName = new AssemblyName(evaluatedInclude);
            string metadataValue = unresolved.GetMetadataValue("HintPath");

            return new UnresolvedAssemblyReferenceMoniker(assemblyName, metadataValue);
        }
    }
}
