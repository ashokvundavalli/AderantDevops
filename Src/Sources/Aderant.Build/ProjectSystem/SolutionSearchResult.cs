using System.Collections.Generic;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem {
    /// <summary>
    /// The results of a solution search.
    /// </summary>
    internal class SolutionSearchResult {

        public SolutionSearchResult(string file, ProjectInSolutionWrapper project) {
            SolutionFile = file;
            Project = project;
            Found = true;
        }

        /// <summary>
        /// Gets the solution file path.
        /// </summary>
        /// <value>The solution file.</value>
        public string SolutionFile { get; private set; }

        /// <summary>
        /// Gets the project referenced by the solution at <see cref="SolutionFile" />
        /// </summary>
        /// <value>The project.</value>
        public ProjectInSolutionWrapper Project { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether a solution was found.
        /// </summary>
        public bool Found { get; internal set; }
    }

    /// <summary>
    /// Isolation seam between MS Build object model and the build tree.
    /// </summary>
    internal class ProjectConfigurationInSolutionWrapper {

        internal ProjectConfigurationInSolutionWrapper(ProjectConfigurationInSolution configuration)
            : this(configuration.ConfigurationName, configuration.PlatformName, configuration.FullName, configuration.IncludeInBuild) {
        }

        internal ProjectConfigurationInSolutionWrapper(string configurationName = null, string platformName = null, string fullName = null, bool includeInBuild = false) {
            ConfigurationName = configurationName;
            PlatformName = platformName;
            FullName = fullName;
            IncludeInBuild = includeInBuild;
        }

        public string ConfigurationName { get; }
        public string PlatformName { get; }
        public string FullName { get; }
        public bool IncludeInBuild { get; }
    }

    /// <summary>
    /// Isolation seam between MS Build object model and the build tree.
    /// </summary>
    internal class ProjectInSolutionWrapper {
        private readonly ProjectInSolution project;

        internal ProjectInSolutionWrapper(ProjectInSolution project) {
            this.project = project;
            if (project != null) {
                ProjectConfigurations = Wrap(project.ProjectConfigurations);
            }
        }

        public string ProjectName => project.ProjectName;
        public string RelativePath => project.RelativePath;
        public string AbsolutePath => project.AbsolutePath;
        public string ProjectGuid => project.ProjectGuid;
        public string ParentProjectGuid => project.ParentProjectGuid;
        public IReadOnlyList<string> Dependencies => project.Dependencies;

        public IReadOnlyDictionary<string, ProjectConfigurationInSolutionWrapper> ProjectConfigurations { get; internal set; }

        private static IReadOnlyDictionary<string, ProjectConfigurationInSolutionWrapper> Wrap(IReadOnlyDictionary<string, ProjectConfigurationInSolution> source) {
            var dictionary = new Dictionary<string, ProjectConfigurationInSolutionWrapper>();

            foreach (var key in source.Keys) {
                dictionary.Add(key, new ProjectConfigurationInSolutionWrapper(source[key]));
            }

            return dictionary;
        }
    }
}
