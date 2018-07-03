using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Aderant.Build.ProjectSystem.References;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    /// <summary>
    /// Class ConfiguredProject.
    /// </summary>
    [Export(typeof(ConfiguredProject))]
    [ExportMetadata("Scope", nameof(ConfiguredProject))]
    internal class ConfiguredProject {
        private readonly IFileSystem fileSystem;
        private readonly IProjectTree tree;
        private Lazy<Project> project;
        private Lazy<ProjectRootElement> projectXml;

        [ImportingConstructor]
        public ConfiguredProject(IProjectTree tree, IFileSystem fileSystem) {
            this.tree = tree;
            this.fileSystem = fileSystem;
        }

        public Guid ProjectGuid {
            get {
                foreach (var propertyElement in projectXml.Value.Properties) {
                    if (propertyElement.Name == "ProjectGuid") {
                        if (propertyElement.Value != null) {
                            return Guid.Parse(propertyElement.Value);
                        }
                    }
                }

                return Guid.Empty;
            }
        }

        [Import]
        private Lazy<IConfiguredProjectServices> ServicesImport { get; set; }

        public IConfiguredProjectServices Services {
            get { return ServicesImport.Value; }
        }

        public string FullPath { get; private set; }

        /// <summary>
        /// Gets or sets the solution file which contains this project.
        /// </summary>
        /// <value>The solution file.</value>
        public string SolutionFile { get; set; }

        public bool IncludeInBuild { get; set; }

        public void InitializeAsync(Lazy<ProjectRootElement> projectXml, string fullPath) {
            FullPath = fullPath;
            this.projectXml = projectXml;

            project = new Lazy<Project>(() => new Project(this.projectXml.Value, null, null, new ProjectCollection()));
        }

        public ICollection<ProjectItem> GetItems(string itemType) {
            return project.Value.GetItems(itemType);
        }

        public void AssignProjectConfiguration(string buildConfiguration) {
            var projectInSolution = tree.SolutionManager.GetSolutionForProject(FullPath, ProjectGuid);

            SolutionFile = projectInSolution.SolutionFile;

            ProjectConfigurationInSolution projectConfigurationInSolution;
            if (projectInSolution.Project.ProjectConfigurations.TryGetValue(buildConfiguration, out projectConfigurationInSolution)) {
                IncludeInBuild = projectConfigurationInSolution.IncludeInBuild;
            }

            tree.AddConfiguredProject(this);
        }

        /// <summary>
        /// Collects the build dependencies required to build the artifacts in this result.
        /// </summary>
        public Task CollectBuildDependencies(BuildDependenciesCollector buildDependenciesCollector) {
            // Force MEF import
            var services = Services;

            // OK boots, start walking...
            var t1 = Task.Run(
                () => {
                    if (Services.TextTemplateReferences != null) {
                        IReadOnlyCollection<IUnresolvedReference> references = services.TextTemplateReferences.GetUnresolvedReferences();
                        if (references != null) {
                            buildDependenciesCollector.AddUnresolvedReferences(references);
                        }
                    }
                });

            var t2 = Task.Run(
                () => {
                    if (Services.ProjectReferences != null) {
                        IReadOnlyCollection<IUnresolvedReference> references = services.ProjectReferences.GetUnresolvedReferences();
                        if (references != null) {
                            buildDependenciesCollector.AddUnresolvedReferences(references);
                        }
                    }
                });

            var t3 = Task.Run(
                () => {
                    if (Services.AssemblyReferences != null) {
                        IReadOnlyCollection<IUnresolvedReference> references = services.AssemblyReferences.GetUnresolvedReferences();
                        if (references != null) {
                            buildDependenciesCollector.AddUnresolvedReferences(references);
                        }
                    }
                });

            return Task.WhenAll(t1, t2, t3);
        }
    }
}
