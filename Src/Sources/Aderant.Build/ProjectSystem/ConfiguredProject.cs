﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;
using Aderant.Build.Utilities;
using Aderant.Build.VersionControl.Model;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem {
    /// <summary>
    /// A project that has been scanned, configured and accepted by the build.
    /// </summary>
    [Export(typeof(ConfiguredProject))]
    [ExportMetadata("Scope", nameof(ConfiguredProject))]
    [DebuggerDisplay("{ProjectGuid}::{FullPath}")]
    internal class ConfiguredProject : AbstractArtifact, IReference, IBuildDependencyProjectReference, IAssemblyReference {
        private List<string> dirtyFiles;

        private Memoizer<ConfiguredProject, IReadOnlyList<Guid>> extractTypeGuids;
        private Memoizer<ConfiguredProject, bool> isWebProject;

        private Lazy<Project> project;
        private Memoizer<ConfiguredProject, Guid> projectGuid;
        private Lazy<ProjectRootElement> projectXml;

        [ImportingConstructor]
        public ConfiguredProject(IProjectTree tree) {
            Tree = tree;
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

        public virtual string OutputAssembly {
            get { return project.Value.GetPropertyValue("AssemblyName"); }
        }

        public virtual string OutputType {
            get { return project.Value.GetPropertyValue("OutputType"); }
        }

        internal IReadOnlyList<Guid> ProjectTypeGuids {
            get {
                if (extractTypeGuids != null) {
                    return extractTypeGuids.Evaluate(this);
                }

                return new Guid[0];
            }
        }

        public bool IsWebProject {
            get { return isWebProject.Evaluate(this); }
        }

        public bool IsTestProject {
            get {
                var guids = ProjectTypeGuids;
                if (guids != null) {
                    if (guids.Contains(WellKnownProjectTypeGuids.TestProject)) {
                        return true;
                    }
                }

                if (OutputAssembly.Contains("UIAutomation")) {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the directory that roots this project.
        /// Driven from the path of <see cref="SolutionFile" />
        /// </summary>
        public string SolutionRoot {
            get {
                if (SolutionFile != null) {
                    return Path.GetDirectoryName(SolutionFile);
                }

                return null;
            }
        }

        public ProjectBuildConfiguration BuildConfiguration { get; set; }

        /// <summary>
        /// Flag for if the project has been changed.
        /// Used for reducing the build set.
        /// </summary>
        public bool IsDirty { get; set; }

        public IReadOnlyList<string> DirtyFiles {
            get { return dirtyFiles; }
        }

        internal BuildReason BuildReason { get; set; }

        /// <summary>
        /// Gets the evaluated output path
        /// </summary>
        public string OutputPath { get; internal set; }

        /// <summary>
        /// Indicates that a common output directory is used.
        /// Prevents GetCopyToOutputDirectoryItems in Microsoft.Common.CurrentVersion.targets from including too much transitive
        /// baggage
        /// </summary>
        public bool UseCommonOutputDirectory { get; set; }

        public virtual Guid ProjectGuid {
            get { return projectGuid.Evaluate(this); }
        }

        public override string Id {
            get { return GetAssemblyName(); }
        }

        public string GetAssemblyName() {
            return OutputAssembly;
        }

        public void Initialize(Lazy<ProjectRootElement> projectElement, string fullPath) {
            FullPath = fullPath;
            projectXml = projectElement;

            project = new Lazy<Project>(
                () => {
                    IDictionary<string, string> globalProperties = new Dictionary<string, string> {
                        { "WebDependencyVersion", "-1" }
                    };
                    
                    return new Project(projectXml.Value, globalProperties, null, CreateProjectCollection(), ProjectLoadSettings.IgnoreMissingImports);
                });

            extractTypeGuids = new Memoizer<ConfiguredProject, IReadOnlyList<Guid>>(
                configuredProject => {
                    var propertyElement = project.Value.GetPropertyValue("ProjectTypeGuids");

                    if (!string.IsNullOrEmpty(propertyElement)) {
                        var guids = propertyElement.Split(';');
                        var guidList = new List<Guid>();

                        guids.Aggregate(
                            guidList,
                            (list, s) => {
                                Guid result;
                                if (Guid.TryParse(s, out result)) {
                                    list.Add(result);
                                }

                                return list;
                            });

                        return guidList;
                    }

                    return new Guid[0];
                },
                EqualityComparer<object>.Default);

            isWebProject = new Memoizer<ConfiguredProject, bool>(
                configuredProject => {

                    var guids = ProjectTypeGuids;
                    if (guids != null) {
                        return guids.Intersect(WellKnownProjectTypeGuids.WebProjectGuids).Any();
                    }

                    return false;
                },
                EqualityComparer<object>.Default);

            projectGuid = new Memoizer<ConfiguredProject, Guid>(
                configuredProject => {

                    var propertyElement = project.Value.GetPropertyValue("ProjectGuid");
                    if (propertyElement != null) {
                        try {
                            return Guid.Parse(propertyElement);
                        } catch (FormatException ex) {
                            throw new FormatException(ex.Message + " " + propertyElement + " in " + FullPath, ex);
                        }
                    }

                    return Guid.Empty;
                },
                EqualityComparer<object>.Default);
        }

        private static ProjectCollection CreateProjectCollection() {
            var collection = new ProjectCollection {
                IsBuildEnabled = false,
                DisableMarkDirty = true,
            };

            return collection;
        }

        public ICollection<ProjectItem> GetItems(string itemType) {
            return project.Value.GetItems(itemType);
        }

        public void AssignProjectConfiguration(ConfigurationToBuild solutionBuildConfiguration) {
            var projectInSolution = Tree.SolutionManager.GetSolutionForProject(FullPath, ProjectGuid);

            if (projectInSolution.Found) {
                SolutionFile = projectInSolution.SolutionFile;

                ProjectConfigurationInSolutionWrapper projectConfigurationInSolution;
                if (projectInSolution.Project.ProjectConfigurations.TryGetValue(solutionBuildConfiguration.FullName, out projectConfigurationInSolution)) {
                    IncludeInBuild = projectConfigurationInSolution.IncludeInBuild;

                    // GC optimization
                    BuildConfiguration = ProjectBuildConfiguration.GetConfiguration(projectConfigurationInSolution.ConfigurationName, projectConfigurationInSolution.PlatformName);

                    if (BuildConfiguration == null) {
                        BuildConfiguration = new ProjectBuildConfiguration(projectConfigurationInSolution.ConfigurationName, projectConfigurationInSolution.PlatformName);
                    }
                }

                if (IncludeInBuild) {
                    Tree.AddConfiguredProject(this);

                    SetOutputPath();
                }
            } else {
                IProjectTreeInternal treeInternal = Tree as IProjectTreeInternal;
                if (treeInternal != null) {
                    treeInternal.OrphanProject(this);
                }
            }
        }

        private void SetOutputPath() {
            if (project != null) {
                var projectValue = project.Value;

                projectValue.SetProperty("Configuration", BuildConfiguration.ConfigurationName);
                projectValue.SetProperty("Platform", BuildConfiguration.PlatformName);
                projectValue.ReevaluateIfNecessary();

                OutputPath = projectValue.GetPropertyValue("OutputPath");
            }

        }

        /// <summary>
        /// Collects the build dependencies required to build the artifacts in this result.
        /// </summary>
        public Task CollectBuildDependencies(BuildDependenciesCollector collector) {

            // Allows unit testing
            if (ServicesImport != null) {
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

            return Task.CompletedTask;
        }

        public void AnalyzeBuildDependencies(BuildDependenciesCollector collector) {
            // Force MEF import
            var services = Services;

            if (services.ProjectReferences != null) {
                var results = services.ProjectReferences.GetResolvedReferences(collector.UnresolvedReferences);
                if (results != null) {
                    foreach (var reference in results) {
                        AddResolvedDependency(reference.ExistingUnresolvedItem, reference.ResolvedReference);
                    }
                }
            }

            if (services.AssemblyReferences != null) {
                var results = services.AssemblyReferences.GetResolvedReferences(collector.UnresolvedReferences);

                // Because assembly references can be replaced by a dependency on another project
                // we need to check if this has happened and unpack the result
                if (results != null) {
                    foreach (var reference in results) {
                        AddResolvedDependency(reference.ExistingUnresolvedItem, reference.ResolvedReference);
                    }
                }
            }
        }

        public void CalculateDirtyStateFromChanges(IReadOnlyCollection<ISourceChange> changes) {
            MarkThisFileDirty(changes);

            if (IsDirty) {
                return;
            }

            // check if this proj contains needed files
            List<ProjectItem> items = new List<ProjectItem>();

            items.AddRange(project.Value.GetItems("Compile"));
            items.AddRange(project.Value.GetItems("Content"));
            items.AddRange(project.Value.GetItems("None"));

            foreach (var item in items) {
                foreach (var file in changes) {
                    string value = item.GetMetadataValue("FullPath");

                    if (string.Equals(value, file.FullPath, StringComparison.OrdinalIgnoreCase)) {
                        MarkDirty();

                        if (dirtyFiles == null) {
                            dirtyFiles = new List<string>();
                        }

                        dirtyFiles.Add(file.Path);
                        return;
                    }
                }
            }
        }

        private void MarkDirty() {
            IsDirty = true;
            this.SetReason(BuildReasonTypes.ProjectFileChanged);
        }

        private void MarkThisFileDirty(IReadOnlyCollection<ISourceChange> changes) {
            foreach (var change in changes) {
                if (string.Equals(FullPath, change.FullPath, StringComparison.OrdinalIgnoreCase)) {
                    MarkDirty();
                    return;
                }
            }
        }

        public string GetOutputAssemblyWithExtension() {
            if (string.Equals(OutputType, "Library", StringComparison.OrdinalIgnoreCase)) {
                return OutputAssembly + ".dll";
            }

            if (string.Equals(OutputType, "winexe", StringComparison.OrdinalIgnoreCase)) {
                return OutputAssembly + ".exe";
            }

            if (string.Equals(OutputType, "exe", StringComparison.OrdinalIgnoreCase)) {
                return OutputAssembly + ".exe";
            }

            throw new NotSupportedException("Unable to determine output extension from type:" + OutputType);
        }
    }

    internal class BuildReason {
        public string Tag { get; set; }
        public BuildReasonTypes Flags { get; set; }
    }

    internal static class BuildReasonExtensions {

        public static void SetReason(this ConfiguredProject project, BuildReasonTypes reasonTypes, string tag = null) {
            if (project.BuildReason == null) {
                project.BuildReason = new BuildReason { Flags = reasonTypes };
            } else {
                project.BuildReason.Flags |= reasonTypes;
            }

            if (tag != null) {
                project.BuildReason.Tag = tag;
            }
        }
    }

}
