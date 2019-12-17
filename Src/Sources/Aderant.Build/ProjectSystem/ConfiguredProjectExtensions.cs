using System.Linq;

namespace Aderant.Build.ProjectSystem {
    internal static class ConfiguredProjectExtensions {

        public static bool IsWorkflowProject(this ConfiguredProject project) {
            return project.ProjectTypeGuids != null && project.ProjectTypeGuids.Contains(WellKnownProjectTypeGuids.WorkflowFoundation);
        }

        /// <summary>
        /// Returns true if the project should be given to the build engine.
        /// </summary>
        public static bool RequiresBuilding(this ConfiguredProject project) {
            return project.IncludeInBuild && project.IsDirty;
        }
    }
}