using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.ProjectSystem {
    [Export(typeof(IConfiguredProjectServices))]
    [ExportMetadata("Scope", nameof(ConfiguredProject))]
    internal class ConfiguredProjectServices : ProjectServices, IConfiguredProjectServices, IProjectCommonServices {

        [ImportMany()]
        private ICollection<IAssemblyReferencesService> assemblyReferences;

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
            assemblyReferences = new List<IAssemblyReferencesService>();

            this.configuredProject = configuredProject;
        }

        /// <summary>
        /// Gets the assembly references service.
        /// </summary>
        public IAssemblyReferencesService AssemblyReferences {
            get { return new AggregateReferenceService(assemblyReferences); }
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

    internal class AggregateReferenceService : IAssemblyReferencesService {
        private readonly ICollection<IAssemblyReferencesService> assemblyReferences;

        public AggregateReferenceService(ICollection<IAssemblyReferencesService> assemblyReferences) {
            this.assemblyReferences = assemblyReferences;
        }

        public IReadOnlyCollection<IUnresolvedAssemblyReference> GetUnresolvedReferences() {
            List< IUnresolvedAssemblyReference> unresolvedReferences= new List<IUnresolvedAssemblyReference>();
            foreach (var service in assemblyReferences) {
                unresolvedReferences.AddRange(service.GetUnresolvedReferences());
            }

            return unresolvedReferences;
        }

        public IReadOnlyCollection<ResolvedDependency<IUnresolvedAssemblyReference, IAssemblyReference>> GetResolvedReferences(IReadOnlyCollection<IUnresolvedReference> references, Dictionary<string, string> aliasMap) {
            List<ResolvedDependency<IUnresolvedAssemblyReference, IAssemblyReference>> unresolvedReferences = new List<ResolvedDependency<IUnresolvedAssemblyReference, IAssemblyReference>>();
            foreach (var service in assemblyReferences) {
                unresolvedReferences.AddRange(service.GetResolvedReferences(references, aliasMap));
            }

            return unresolvedReferences;
        }

        public IAssemblyReference SynthesizeResolvedReferenceForProjectOutput(IUnresolvedAssemblyReference unresolved) {
            foreach (var service in assemblyReferences) {
                var result = service.SynthesizeResolvedReferenceForProjectOutput(unresolved);
                if (result != null) {
                    return result;
                }
            }

            return null;
        }
    }
}
