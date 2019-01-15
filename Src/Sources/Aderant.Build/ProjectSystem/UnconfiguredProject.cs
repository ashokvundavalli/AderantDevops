﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Aderant.Build.Utilities;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    [Export(typeof(UnconfiguredProject))]
    internal class UnconfiguredProject {
        private Lazy<ProjectRootElement> projectElement;
        private Memoizer<UnconfiguredProject, Guid> projectGuid;
        private bool isTemplateProject;

        public UnconfiguredProject() {
        }

        public Guid ProjectGuid {
            get { return projectGuid.Evaluate(this); }
        }

        [Import]
        internal ExportFactory<ConfiguredProject> ConfiguredProjectFactory { get; set; }

        public string FullPath { get; private set; }

        public bool IsTemplateProject() {
            if (isTemplateProject) {
                return isTemplateProject;
            }

            return projectElement != null && projectElement.Value == null;
        }

        public void Initialize(XmlReader reader, string projectLocation) {
            FullPath = projectLocation;

            // CreateReader from XDocument doesn't work with ProjectRootElement.Create so use the old XmlDocument API
            var projectDocument = new XmlDocument();
            projectDocument.Load(reader);

            projectElement = new Lazy<ProjectRootElement>(
                () => {
                    ProjectCollection projectCollection;

                    if (ProjectCollection == null) {
                        // Create a project collection for each project since the toolset might change depending on the type of project
                        projectCollection = CreateProjectCollection();
                        projectCollection.SkipEvaluation = true;
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

                    if (!string.IsNullOrEmpty(FullPath)) {
                        element.FullPath = projectLocation;
                    } else {
                        element.FullPath = Path.GetRandomFileName();
                    }

                    return element;

                }, LazyThreadSafetyMode.PublicationOnly);


            this.projectGuid = new Memoizer<UnconfiguredProject, Guid>(
                project => {
                    foreach (var propertyElement in project.projectElement.Value.Properties) {
                        if (propertyElement.Name == "ProjectGuid") {
                            if (propertyElement.Value != null) {
                                return Guid.Parse(propertyElement.Value);
                            }
                        }
                    }
                    return Guid.Empty;
                }, EqualityComparer<object>.Default);
        }

        public ProjectCollection ProjectCollection { get; set; }

        private ProjectCollection CreateProjectCollection() {
            ProjectCollection projectCollection = new ProjectCollection();
            return projectCollection;
        }

        public virtual ConfiguredProject LoadConfiguredProject(IProjectTree projectTree) {
            var result = ConfiguredProjectFactory.CreateExport();
            var configuredProject = result.Value;

            configuredProject.Initialize(projectElement, FullPath);
            return configuredProject;
        }
    }
}
