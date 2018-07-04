using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    /// <summary>
    /// Class ConfiguredProject.
    /// </summary>
    [Export(typeof(ConfiguredProject))]
    [ExportMetadata("Scope", nameof(ConfiguredProject))]
    [DebuggerDisplay("{ProjectGuid}::{FullPath}")]
    internal class ConfiguredProject : IArtifact, IReference {
        private readonly IFileSystem fileSystem;
        private Lazy<Project> project;
        private Lazy<ProjectRootElement> projectXml;
        private List<ResolvedReference> resolvedDependencies;

        [ImportingConstructor]
        public ConfiguredProject(IProjectTree tree, IFileSystem fileSystem) {
            this.Tree = tree;
            this.fileSystem = fileSystem;
        }

        [Import]
        private Lazy<IConfiguredProjectServices> ServicesImport { get; set; }

        public IConfiguredProjectServices Services {
            get { return ServicesImport.Value; }
        }

        public string FullPath { get; private set; }

        public string Id {
            get { return FullPath; }
        }

        /// <summary>
        /// Gets or sets the solution file which contains this project.
        /// </summary>
        /// <value>The solution file.</value>
        public string SolutionFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this project is included in build.
        /// The project can be excluded as it does not have a platform or configuration for the current build.
        /// </summary>
        public bool IncludeInBuild { get; set; }

        /// <summary>
        /// Gets the tree this project belongs to.
        /// </summary>
        /// <value>The tree.</value>
        public IProjectTree Tree { get; }

        public string OutputAssembly {
            get { return project.Value.GetPropertyValue("AssemblyName"); }
        }

        public string OutputType {
            get { return project.Value.GetPropertyValue("OutputType"); }
        }

        public Guid ProjectGuid {
            get {
                var propertyElement = project.Value.GetPropertyValue("ProjectGuid");
                if (propertyElement != null) {
                    return Guid.Parse(propertyElement);
                }

                return Guid.Empty;
            }
        }

        public void Initialize(Lazy<ProjectRootElement> projectElement, string fullPath) {
            FullPath = fullPath;
            projectXml = projectElement;
            resolvedDependencies = new List<ResolvedReference>();

            project = new Lazy<Project>(() => new Project(this.projectXml.Value, null, null, new ProjectCollection()));
        }

        public ICollection<ProjectItem> GetItems(string itemType) {
            return project.Value.GetItems(itemType);
        }

        public void AssignProjectConfiguration(string buildConfiguration) {
            var projectInSolution = Tree.SolutionManager.GetSolutionForProject(FullPath, ProjectGuid);

            SolutionFile = projectInSolution.SolutionFile;

            ProjectConfigurationInSolution projectConfigurationInSolution;
            if (projectInSolution.Project.ProjectConfigurations.TryGetValue(buildConfiguration, out projectConfigurationInSolution)) {
                IncludeInBuild = projectConfigurationInSolution.IncludeInBuild;
            }

            if (IncludeInBuild) {
                Tree.AddConfiguredProject(this);
            }
        }

        /// <summary>
        /// Collects the build dependencies required to build the artifacts in this result.
        /// </summary>
        public Task CollectBuildDependencies(BuildDependenciesCollector collector) {
            // Force MEF import
            var services = Services;

            // OK boots, start walking...
            var t1 = Task.Run(
                () => {
                    if (Services.TextTemplateReferences != null) {
                        IReadOnlyCollection<IUnresolvedReference> references = services.TextTemplateReferences.GetUnresolvedReferences();
                        if (references != null) {
                            collector.AddUnresolvedReferences(references);
                        }
                    }
                });

            var t2 = Task.Run(
                () => {
                    if (Services.ProjectReferences != null) {
                        IReadOnlyCollection<IUnresolvedReference> references = services.ProjectReferences.GetUnresolvedReferences();
                        if (references != null) {
                            collector.AddUnresolvedReferences(references);
                        }
                    }
                });

            var t3 = Task.Run(
                () => {
                    if (Services.AssemblyReferences != null) {
                        IReadOnlyCollection<IUnresolvedReference> references = services.AssemblyReferences.GetUnresolvedReferences();
                        if (references != null) {
                            collector.AddUnresolvedReferences(references);
                        }
                    }
                });

            return Task.WhenAll(t1, t2, t3);
        }

        public void AnalyzeBuildDependencies(BuildDependenciesCollector collector) {
            // Force MEF import
            var services = Services;

            if (services.ProjectReferences != null) {
                var results = services.ProjectReferences.GetResolvedReferences(collector.UnresolvedReferences);
                if (results != null) {
                    foreach (var reference in results) {
                        AddResolvedDependency(collector, reference.ExistingUnresolvedItem, reference.ResolvedReference);
                    }
                }
            }

            if (services.AssemblyReferences != null) {
                var results = services.AssemblyReferences.GetResolvedReferences(collector.UnresolvedReferences);
                if (results != null) {
                    foreach (var reference in results) {
                        AddResolvedDependency(collector, reference.ExistingUnresolvedItem, reference.ResolvedReference);
                    }
                }
            }
        }

        private void AddResolvedDependency(BuildDependenciesCollector collector, IUnresolvedReference existingUnresolvedItem, IReference dependency) {
            collector.AddResolvedDependency(existingUnresolvedItem, dependency);

            var dependencies = resolvedDependencies;

            if (dependencies != null) {
                resolvedDependencies.Add(new ResolvedReference(this, existingUnresolvedItem, dependency));
            }
        }

        public IReadOnlyCollection<IDependable> GetDependencies() {
            return resolvedDependencies.Select(s => s.ResolvedReference).ToList();
        }

        /// <summary>
        /// Replaces resolved dependencies with an equivalent one from the provided set.
        /// </summary>
        public void ReplaceDependencies(IReadOnlyCollection<IDependable> dependables, IEqualityComparer<IDependable> comparer) {
            foreach (var r in resolvedDependencies) {
                foreach (var item in dependables) {
                    if (comparer.Equals(r.ResolvedReference, item)) {
                        r.ReplaceReference(item);
                    }
                }
            }
        }
    }
}
