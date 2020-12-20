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

        private bool isTemplateProject;
        private Lazy<ProjectRootElement> projectElement;
        private Memoizer<UnconfiguredProject, Guid> projectGuid;

        public UnconfiguredProject() {
            projectGuid = new Memoizer<UnconfiguredProject, Guid>(
                project => {
                    foreach (var propertyElement in project.projectElement.Value.Properties) {
                        if (propertyElement.Name == "ProjectGuid") {
                            if (propertyElement.Value != null) {
                                return Guid.Parse(propertyElement.Value);
                            }
                        }
                    }

                    return Guid.Empty;
                });
        }

        public Guid ProjectGuid {
            get { return projectGuid.Evaluate(this); }
        }

        [Import]
        internal ExportFactory<ConfiguredProject> ConfiguredProjectFactory { get; set; }

        public string FullPath { get; private set; }

        public ProjectCollection ProjectCollection { get; set; }

        public bool AllowConformityModification { get; set; }

        /// <summary>
        /// The path to the WIX tool chain targets file (optional)
        /// </summary>
        public string WixTargetsPath { get; set; }

        public bool IsTemplateProject() {
            if (isTemplateProject) {
                return isTemplateProject;
            }

            return projectElement != null && projectElement.Value == null;
        }

        public void Initialize(XmlReader reader, string projectLocation) {
            FullPath = projectLocation;

            ProjectCollection projectCollection;

            if (ProjectCollection == null) {
                // Create a project collection for each project since the toolset might change depending on the type of project
                projectCollection = CreateProjectCollection();
            } else {
                projectCollection = ProjectCollection;
            }

            InitializeInternal(this, reader, projectLocation, projectCollection);
        }

        private void InitializeInternal(UnconfiguredProject unconfiguredProject, XmlReader reader, string projectLocation, ProjectCollection projectCollection) {
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
                    return CreateProjectRootElementFromReader(projectDocument, projectLocation, unconfiguredProject, projectCollection);
                }, LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// Factory method for creating a Project Root Element.
        /// </summary>
        /// <param name="projectDocument">The project XML.</param>
        /// <param name="projectLocation">The optional on disk location.</param>
        /// <param name="unconfiguredProject">The optional unconfigured project that provides the evaluation context.</param>
        /// <param name="projectCollection">The optional global project location.</param>
        /// <returns></returns>
        internal static ProjectRootElement CreateProjectRootElementFromReader(XmlDocument projectDocument, string projectLocation = null, UnconfiguredProject unconfiguredProject = null, ProjectCollection projectCollection = null) {
            using (XmlNodeList elementsByTagName = projectDocument.GetElementsByTagName("TargetFrameworkVersion")) {
                foreach (XmlElement item in elementsByTagName) {
                    if (string.Equals(item.InnerText, "v$targetframeworkversion$", StringComparison.OrdinalIgnoreCase)) {
                        if (unconfiguredProject != null) {
                            unconfiguredProject.isTemplateProject = true;
                        }

                        return null;
                    }
                }
            }

            if (projectCollection == null) {
                // Create a project collection for each project since the toolset might change depending on the type of project
                projectCollection = CreateProjectCollection();
            }

            using (XmlReader xmlReader = new XmlNodeReader(projectDocument)) {
                var element = ProjectRootElement.Create(xmlReader, projectCollection);

                AssignPath(projectLocation, element);

                ProcessImports(unconfiguredProject, projectLocation, element);

                return element;
            }
        }

        private static void AssignPath(string projectLocation, ProjectRootElement element) {
            if (!string.IsNullOrEmpty(projectLocation)) {
                element.FullPath = projectLocation;
            } else {
                element.FullPath = Path.GetRandomFileName();
            }
        }

        private static void ProcessImports(UnconfiguredProject unconfiguredProject, string projectLocation, ProjectRootElement element) {
            // PERF: We don't care about any imports, just the base project data
            var imports = element.Imports.ToList();

            var importsToRemove = new List<ProjectImportElement>();

            foreach (var import in imports) {
                if (import.Project.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) {
                    importsToRemove.Add(import);
                    continue;
                }

                if (import.Project.IndexOf("MSTest_V2_Common", StringComparison.OrdinalIgnoreCase) >= 0) {
                    importsToRemove.Add(import);
                }
            }

            if (unconfiguredProject != null && unconfiguredProject.AllowConformityModification) {
                if (!string.IsNullOrEmpty(projectLocation)) {
                    var controller = new ProjectConformityController();
                    controller.AddDirProjectIfNecessary(element, projectLocation);
                }
            }

            foreach (var import in importsToRemove) {
                element.RemoveChild(import);
            }
        }

        private static ProjectCollection CreateProjectCollection() {
            var projectCollection = new ProjectCollection();
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

            configuredProject.WixTargetsPath = WixTargetsPath;
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
