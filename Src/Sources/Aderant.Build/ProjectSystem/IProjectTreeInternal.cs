using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    internal interface IProjectTreeInternal {
        void OrphanProject(ConfiguredProject configuredProject);

        /// <summary>
        /// Returns a collection to associate a <see cref="ConfiguredProject"/> instance with.
        /// </summary>
        /// <returns></returns>
        ProjectCollection GetProjectCollection();
    }
}
