using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Xml;
using Aderant.Build.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    [Export(typeof(UnconfiguredProject))]
    internal class UnconfiguredProject {
        private Lazy<ProjectRootElement> projectXml;
        private Memoizer<UnconfiguredProject, Guid> projectGuid;
   
        public UnconfiguredProject() {
        }

        public Guid ProjectGuid {
            get { return projectGuid.Evaluate(this); }
        }

        [Import]
        internal ExportFactory<ConfiguredProject> ConfiguredProjectFactory { get; set; }

        public string FullPath { get; private set; }

        public void Initialize(XmlReader reader, string projectLocation) {
            FullPath = projectLocation;

            // Create a project collection for each project since the toolset might change depending on the type of project
            ProjectCollection projectCollection = CreateProjectCollection();

            projectXml = new Lazy<ProjectRootElement>(
                () => {
                    if (!string.IsNullOrEmpty(projectLocation)) {
                        return ProjectRootElement.Open(projectLocation, projectCollection);
                    }

                    return ProjectRootElement.Create(reader, projectCollection);
                }, LazyThreadSafetyMode.PublicationOnly);


            this.projectGuid = new Memoizer<UnconfiguredProject, Guid>(
                project => {
                    foreach (var propertyElement in project.projectXml.Value.Properties) {
                        if (propertyElement.Name == "ProjectGuid") {
                            if (propertyElement.Value != null) {
                                return Guid.Parse(propertyElement.Value);
                            }
                        }
                    }
                    return Guid.Empty;
                }, EqualityComparer<object>.Default);
        }

        private ProjectCollection CreateProjectCollection() {
            ProjectCollection projectCollection = new ProjectCollection();
            return projectCollection;
        }

        public virtual ConfiguredProject LoadConfiguredProject(IProjectTree projectTree) {
            var result = ConfiguredProjectFactory.CreateExport();
            var configuredProject = result.Value;

            configuredProject.Initialize(projectXml, FullPath);
            return configuredProject;
        }
    }
}
