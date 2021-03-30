using System;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.ProjectSystem {

    /// <summary>
    /// First level cache for parsed solution information
    /// Also stores the data needed to create the "CurrentSolutionConfigurationContents" object that
    /// MSBuild uses when building from a solution. Unfortunately the build engine doesn't let us build just projects
    /// due to the way that AssignProjectConfiguration in Microsoft.Common.CurrentVersion.targets assumes that you are coming from a solution
    /// and attempts to assign platforms and targets to project references, even if you are not building them.
    /// </summary>
    internal class ProjectToSolutionMap {

        private Dictionary<string, ProjectInSolutionWrapper> projectsOnDisk = new Dictionary<string, ProjectInSolutionWrapper>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<Guid, ProjectInSolutionWrapper> projectsByGuid = new Dictionary<Guid, ProjectInSolutionWrapper>();

        private Dictionary<ProjectInSolutionWrapper, string> projectToSolutionMap = new Dictionary<ProjectInSolutionWrapper, string>();

        public IEnumerable<ProjectInSolutionWrapper> AllProjects {
            get {
                return projectsOnDisk.Values;
            }
        }

        public ProjectInSolutionWrapper GetProjectInSolution(string key) {
            ErrorUtilities.IsNotNull(key, nameof(key));

            ProjectInSolutionWrapper pis;
            projectsOnDisk.TryGetValue(key, out pis);
            return pis;
        }

        public string GetSolution(ProjectInSolutionWrapper projectInSolution) {
            if (projectInSolution == null) {
                return null;
            }

            string solution;
            projectToSolutionMap.TryGetValue(projectInSolution, out solution);
            return solution;
        }

        public bool HasSeenFile(string file) {
            return projectToSolutionMap.Values.FirstOrDefault(s => string.Equals(s, file, StringComparison.OrdinalIgnoreCase)) != null;
        }

        public void AddProject(string projectAbsolutePath, Guid projectGuid, string solutionFile, ProjectInSolutionWrapper wrapper) {
            ErrorUtilities.IsNotNull(projectAbsolutePath, nameof(projectAbsolutePath));
            ErrorUtilities.IsNotNull(wrapper, nameof(wrapper));

            if (!projectsOnDisk.ContainsKey(projectAbsolutePath)) {
                projectsOnDisk.Add(projectAbsolutePath, wrapper);

                if (projectGuid != Guid.Empty && !string.IsNullOrWhiteSpace(solutionFile)) {
                    if (!projectsByGuid.ContainsKey(projectGuid)) {
                        projectsByGuid[projectGuid] = wrapper;
                    } else {
                        // Shared projects maybe referenced by many solutions so this is not an error.
                        if (projectAbsolutePath.EndsWith(".shproj", StringComparison.OrdinalIgnoreCase)) {
                            return;
                        }
                        throw new DuplicateGuidException(projectGuid, $"The project GUID {projectGuid} is already being tracked.");
                    }
                }
            }

            if (!projectToSolutionMap.ContainsKey(wrapper)) {
                projectToSolutionMap.Add(wrapper, solutionFile);
            }

        }
    }
}
