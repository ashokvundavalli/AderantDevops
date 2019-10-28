using System;
using System.ComponentModel.Composition;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.ProjectSystem {
    [Export(typeof(IConfiguredProjectServices))]
    [ExportMetadata("Scope", nameof(ConfiguredProject))]
    internal class ConfiguredProjectServices : ProjectServices, IConfiguredProjectServices, IProjectCommonServices {

        [Import(AllowDefault = true)]
        private Lazy<IAssemblyReferencesService> assemblyReferences;

        private ConfiguredProject configuredProject;

        [Import(AllowDefault = true)]
        private Lazy<IBuildDependencyProjectReferencesService> projectReferences;

        [Import(AllowDefault = true)]
        private Lazy<ITextTemplateReferencesServices> textTemplateReferences;

        [ImportingConstructor]
        internal ConfiguredProjectServices(ConfiguredProject configuredProject) {
            // Constructor to keep the C# compiler happy (unassigned field warning)
            textTemplateReferences = null;
            assemblyReferences = null;
            projectReferences = null;

            this.configuredProject = configuredProject;
        }

        /// <summary>
        /// Gets the assembly references service.
        /// </summary>
        public IAssemblyReferencesService AssemblyReferences {
            get { return assemblyReferences?.Value; }
        }

        /// <summary>
        /// Gets the project reference service.
        /// </summary>
        /// <value>The project references.</value>
        public IBuildDependencyProjectReferencesService ProjectReferences {
            get { return projectReferences?.Value; }
        }

        /// <summary>
        /// Gets the text template reference service.
        /// </summary>
        public ITextTemplateReferencesServices TextTemplateReferences {
            get { return textTemplateReferences?.Value; }
        }
    }
}
