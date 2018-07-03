using System;
using System.ComponentModel.Composition;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    [Export(typeof(UnconfiguredProject))]
    internal class UnconfiguredProject {
        private readonly IProjectTree tree;
        private Lazy<ProjectRootElement> projectXml;

        [ImportingConstructor]
        public UnconfiguredProject(IProjectTree tree) {
            this.tree = tree;
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
        private ExportFactory<ConfiguredProject> ConfiguredProjectFactory { get; set; }

        public string FullPath { get; private set; }

        public void Initialize(XmlReader reader, string projectLocation) {
            FullPath = projectLocation;

            // Create a project collection for each project since the toolset might change depending on the type of project
            ProjectCollection projectCollection = CreateProjectCollection();

            this.projectXml = new Lazy<ProjectRootElement>(
                () =>
                    ProjectRootElement.Open(projectLocation, projectCollection),
                true);
        }

        private ProjectCollection CreateProjectCollection() {
            ProjectCollection projectCollection = new ProjectCollection();
            return projectCollection;
        }

        public ConfiguredProject LoadConfiguredProject(string name) {
            var result = ConfiguredProjectFactory.CreateExport();
            var configuredProject = result.Value;

            configuredProject.InitializeAsync(this.projectXml, FullPath);

            return configuredProject;
        }
    }
}
