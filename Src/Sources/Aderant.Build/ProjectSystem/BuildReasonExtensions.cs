using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.ProjectSystem {
    internal static class BuildReasonExtensions {

        /// <summary>
        /// Sets or amends the <see cref="ConfiguredProject.BuildReason"/> property.
        /// </summary>
        public static void MarkDirtyAndSetReason(this ConfiguredProject project, BuildReasonTypes reasonTypes, string reasonDescription = null) {
            if (project.BuildReason == null) {
                project.BuildReason = new BuildReason { Flags = reasonTypes };
            } else {
                project.BuildReason.Flags |= reasonTypes;
            }

            if (reasonDescription != null) {
                project.BuildReason.Description = reasonDescription;
            }

            project.IsDirty = true;
        }
    }
}