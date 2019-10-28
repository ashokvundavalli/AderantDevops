using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Aderant.Build.Tasks;
using Aderant.Build.Utilities;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    [Export(typeof(UnconfiguredProject))]
    internal class UnconfiguredProject {
        private static XmlNameTable nameTable = new XmlNameTableThreadSafe();

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
        public bool AllowConformityModification { get; set; }

        public bool IsTemplateProject() {
            if (isTemplateProject) {
                return isTemplateProject;
            }

            return projectElement != null && projectElement.Value == null;
        }

        public void Initialize(XmlReader reader, string projectLocation) {
            FullPath = projectLocation;

            // CreateReader from XDocument doesn't work with ProjectRootElement.Create so use the old XmlDocument API
            XmlReaderSettings xmlReaderSettings = new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Ignore,
                NameTable = nameTable
            };

            var projectDocument = new XmlDocument(nameTable);

            try {
                using (XmlReader xmlReader = XmlReader.Create(reader, xmlReaderSettings)) {
                    projectDocument.Load(xmlReader);
                }
            } catch (Exception ex) {
                if (!string.IsNullOrEmpty(projectLocation)) {
                    throw new InvalidProjectFileException(projectLocation, 0, 0, 0, 0, ex.Message, null, null, null);
                }

                throw;
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

                    ProcessImports(projectLocation, element);

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

        private void ProcessImports(string projectLocation, ProjectRootElement element) {
            // PERF: We don't care about any imports, just the base project data
            var imports = element.Imports.ToList();

            var importsToRemove = new List<ProjectImportElement>();

            foreach (var import in imports) {
                if (import.Project.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) {
                    importsToRemove.Add(import);
                }
            }

            if (AllowConformityModification) {
                if (!string.IsNullOrEmpty(projectLocation)) {
                    var controller = new ProjectConformityController();
                    controller.AddDirProjectIfNecessary(element, projectLocation);
                }
            }

            foreach (var import in importsToRemove) {
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

        public static void ClearCaches() {
            nameTable = new XmlNameTableThreadSafe();
        }
    }

    internal class XmlNameTableThreadSafe : NameTable {
        private object locker = new object();

        public override string Add(string key) {
            lock (locker) {
                return base.Add(key);
            }
        }

        public override string Add(char[] key, int start, int len) {
            lock (locker) {
                return base.Add(key, start, len);
            }
        }

        public override string Get(string value) {
            lock (locker) {
                return base.Get(value);
            }
        }

        public override string Get(char[] key, int start, int len) {
            lock (locker) {
                return base.Get(key, start, len);
            }
        }
    }
}