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

        private static AssemblyName failedName = new AssemblyName();

        /// <summary>
        /// Wraps a project item for use in dependency analysis.
        /// </summary>
        public static UnresolvedAssemblyReferenceMoniker Create(ProjectItem unresolved) {
            string evaluatedInclude = unresolved.EvaluatedInclude;
            string metadataValue = unresolved.GetMetadataValue("HintPath");

            AssemblyName assemblyName = null;
            try {
                assemblyName = new AssemblyName(evaluatedInclude);
            } catch (System.IO.FileLoadException) {
                // Handle the case where the name evaluation returns an invalid assembly name string

                return new UnresolvedAssemblyReferenceMoniker(failedName, metadataValue); }

            return new UnresolvedAssemblyReferenceMoniker(assemblyName, metadataValue);
        }
    }
}
