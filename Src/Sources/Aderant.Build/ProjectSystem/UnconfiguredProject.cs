using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Aderant.Build.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    [Export(typeof(UnconfiguredProject))]
    internal class UnconfiguredProject {

        private static readonly Memoizer<UnconfiguredProject, Guid> projectGuidMemoizer = new Memoizer<UnconfiguredProject, Guid>(
            project => {
                foreach (var propertyElement in project.projectElement.Value.Properties) {
                    if (propertyElement.Name == "ProjectGuid") {
                        if (propertyElement.Value != null) {
                            return Guid.Parse(propertyElement.Value);
                        }
                    }
                }

                return Guid.Empty;
            }
        );

        private bool isTemplateProject;
        private Lazy<ProjectRootElement> projectElement;
        private Memoizer<UnconfiguredProject, Guid> projectGuid;

        public UnconfiguredProject() {
        }

        public Guid ProjectGuid {
            get { return projectGuid.Evaluate(this); }
        }

        [Import]
        internal ExportFactory<ConfiguredProject> ConfiguredProjectFactory { get; set; }

        public string FullPath { get; private set; }


        public ProjectCollection ProjectCollection { get; set; }

        public bool IsTemplateProject() {
            if (isTemplateProject) {
                return isTemplateProject;
            }

            return projectElement != null && projectElement.Value == null;
        }

        public void Initialize(XmlReader reader, string projectLocation) {
            FullPath = projectLocation;

            // CreateReader from XDocument doesn't work with ProjectRootElement.Create so use the old XmlDocument API
            XmlReaderSettings xmlReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            var projectDocument = new XmlDocument();

            using (XmlReader xmlReader = XmlReader.Create(reader, xmlReaderSettings)) {
                projectDocument.Load(xmlReader);
            }

            projectElement = new Lazy<ProjectRootElement>(
                () => {
                    ProjectCollection projectCollection;

                    if (ProjectCollection == null) {
                        // Create a project collection for each project since the toolset might change depending on the type of project
                        projectCollection = CreateProjectCollection();
                    } else {
                        projectCollection = ProjectCollection;
                    }

                    using (XmlNodeList elementsByTagName = projectDocument.GetElementsByTagName("TargetFrameworkVersion")) {
                        foreach (XmlElement item in elementsByTagName) {
                            if (string.Equals(item.InnerText, "v$targetframeworkversion$", StringComparison.OrdinalIgnoreCase)) {
                                isTemplateProject = true;
                                return null;
                            }
                        }
                    }

                    ProjectRootElement element;
                    using (XmlReader xmlReader = new XmlNodeReader(projectDocument)) {
                        element = ProjectRootElement.Create(xmlReader, projectCollection);
                    }

                    RemoveImports(element);

                    AssignPath(projectLocation, element);

                    return element;

                },
                LazyThreadSafetyMode.PublicationOnly);

            projectGuid = projectGuidMemoizer;
        }

        private void AssignPath(string projectLocation, ProjectRootElement element) {
            if (!string.IsNullOrEmpty(FullPath)) {
                element.FullPath = projectLocation;
            } else {
                element.FullPath = Path.GetRandomFileName();
            }
        }

        private static void RemoveImports(ProjectRootElement element) {
            // PERF: We don't care about any imports, just the base project data
            var imports = element.Imports.ToList();
            foreach (var import in imports) {
                element.RemoveChild(import);
            }
        }

        private ProjectCollection CreateProjectCollection() {
            ProjectCollection projectCollection = new ProjectCollection();
            projectCollection.SkipEvaluation = true;
            projectCollection.IsBuildEnabled = false;
            return projectCollection;
        }

        public virtual ConfiguredProject LoadConfiguredProject(IProjectTree projectTree) {
            var result = ConfiguredProjectFactory.CreateExport();
            var configuredProject = result.Value;

            IProjectTreeInternal projectTreeInternal = projectTree as IProjectTreeInternal;
            if (projectTreeInternal != null) {
                ProjectCollection projectCollection = projectTreeInternal.GetProjectCollection();

                if (projectCollection != null) {
                    configuredProject.ProjectCollection = projectCollection;
                }
            }

            configuredProject.Initialize(projectElement, FullPath);
            return configuredProject;
        }
    }
}