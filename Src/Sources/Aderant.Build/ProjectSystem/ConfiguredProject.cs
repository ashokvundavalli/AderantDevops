using System;
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

        private static Memoizer<ConfiguredProject, bool> isWebProjectMemoizer = new Memoizer<ConfiguredProject, bool>(
            configuredProject => {
                var guids = configuredProject.ProjectTypeGuids;
                if (guids != null) {
                    return guids.Intersect(WellKnownProjectTypeGuids.WebProjectGuids).Any();
                }

                return false;
            });


        private static Memoizer<ConfiguredProject, string> outputPathMemoizer = new Memoizer<ConfiguredProject, string>(configuredProject => configuredProject.project.Value.GetPropertyValue("OutputType"));

        private static Memoizer<ConfiguredProject, string> outputAssemblyMemoizer = new Memoizer<ConfiguredProject, string>(configuredProject => configuredProject.project.Value.GetPropertyValue("AssemblyName"));

        private static Memoizer<ConfiguredProject, IReadOnlyList<Guid>> extractTypeGuidsMemoizer = new Memoizer<ConfiguredProject, IReadOnlyList<Guid>>(
            args => {
                var propertyElement = args.project.Value.GetPropertyValue("ProjectTypeGuids");

                if (!string.IsNullOrEmpty(propertyElement)) {
                    var guids = propertyElement.Split(';');

                    List<Guid> guidList = new List<Guid>();

                    foreach (var guidString in guids) {
                        Guid result;
                        if (Guid.TryParse(guidString, out result)) {
                            guidList.Add(result);
                        }
                    }

                    return guidList;
                }

                return new Guid[0];
            });

        private static IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "WebDependencyVersion", "-1" },
            { "SolutionDir", "" }
        };

        private List<string> dirtyFiles;

        private Memoizer<ConfiguredProject, IReadOnlyList<Guid>> extractTypeGuids;
        private string fileName;
        private Memoizer<ConfiguredProject, bool> isWebProject;

        private Lazy<Project> project;
        private Memoizer<ConfiguredProject, Guid> projectGuid;

        private Memoizer<ConfiguredProject, Guid> projectGuidMemoizer =
            new Memoizer<ConfiguredProject, Guid>(
                arg => {
                    var propertyElement = arg.project.Value.GetPropertyValue("ProjectGuid");
                    if (propertyElement != null) {
                        try {
                            return Guid.Parse(propertyElement);
                        } catch (FormatException ex) {
                            throw new FormatException(ex.Message + " " + propertyElement + " in " + arg.FullPath, ex);
                        }
                    }

                    return Guid.Empty;
                });

        private List<IResolvedDependency> textTemplateDependencies;

        [ImportingConstructor]
        public ConfiguredProject(IProjectTree tree) {
            Tree = tree;
        }

        [Import]
        private Lazy<IConfiguredProjectServices> ServicesImport { get; set; }

        public IConfiguredProjectServices Services {
            get { return ServicesImport.Value; }
        }

        public virtual string FullPath { get; private set; }

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
            get {
                if (outputPathMemoizer != null) {
                    return outputAssemblyMemoizer.Evaluate(this);
                }

                return null;
            }
        }

        public virtual string OutputType {
            get {
                if (outputPathMemoizer != null) {
                    return outputPathMemoizer.Evaluate(this);
                }

                return null;
            }
        }

        public virtual IReadOnlyList<Guid> ProjectTypeGuids {
            get {
                if (extractTypeGuids != null) {
                    return extractTypeGuids.Evaluate(this);
                }

                return new Guid[0];
            }
            set { extractTypeGuids = new Memoizer<ConfiguredProject, IReadOnlyList<Guid>>(cfg => value); }
        }

        public virtual bool IsWebProject {
            get { return isWebProject.Evaluate(this); }
            set {
                if (value) {
                    isWebProject = Memoizer<ConfiguredProject>.True;
                } else {
                    isWebProject = Memoizer<ConfiguredProject>.False;
                }
            }
        }

        public virtual bool IsTestProject {
            get {
                var guids = ProjectTypeGuids;
                if (guids != null) {
                    if (guids.Contains(WellKnownProjectTypeGuids.TestProject)) {
                        return true;
                    }
                }

                if (OutputAssembly != null && OutputAssembly.Contains("UIAutomation")) {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Flag for if a test project requires testing.
        /// </summary>
        public bool AreTestsImpacted {
            get {
                if (IsTestProject) {
                    if (BuildReason != null) {
                        return BuildReason.Flags != BuildReasonTypes.None;
                    }
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

        public bool IsOfficeProject {
            get {
                var guids = ProjectTypeGuids;
                if (guids != null) {
                    if (guids.Contains(WellKnownProjectTypeGuids.VisualStudioToolsForOffice)) {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Gets a value that indicates if this project is packaged as a zip file.
        /// </summary>
        public bool IsZipPackaged {
            get { return IsWebProject; }
        }

        /// <summary>
        /// The file name portion of <see cref="FullPath" />.
        /// Computed lazily.
        /// </summary>
        public string FileName {
            get { return fileName ?? (fileName = Path.GetFileName(FullPath)); }
        }

        /// <summary>
        /// A project collection to associate with this instance.
        /// </summary>
        internal ProjectCollection ProjectCollection { get; set; }

        public virtual Guid ProjectGuid {
            get { return projectGuid.Evaluate(this); }
        }

        public override string Id {
            get { return FullPath + ":" + GetAssemblyName(); }
        }

        public string GetAssemblyName() {
            return OutputAssembly;
        }

        public void Initialize(Lazy<ProjectRootElement> projectElement, string fullPath) {
            FullPath = fullPath;

            if (projectElement != null) {
                project = InitializeProject(projectElement);

                extractTypeGuids = extractTypeGuidsMemoizer;
                isWebProject = isWebProjectMemoizer;
                projectGuid = projectGuidMemoizer;
            }
        }

        protected virtual Lazy<Project> InitializeProject(Lazy<ProjectRootElement> projectElement) {
            return new Lazy<Project>(() => new Project(projectElement.Value, globalProperties, null, CreateProjectCollection(), ProjectLoadSettings.IgnoreMissingImports));
        }

        private ProjectCollection CreateProjectCollection() {
            if (ProjectCollection != null) {
                return ProjectCollection;
            }

            var collection = new ProjectCollection {
                IsBuildEnabled = false,
                DisableMarkDirty = true,
            };

            return collection;
        }

        public virtual ICollection<ProjectItem> GetItems(string itemType) {
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

                Tree.AddConfiguredProject(this);

                if (IncludeInBuild) {
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
            try {
                // Allows unit testing
                if (ServicesImport != null) {
                    // Force MEF import
                    var services = Services;

                    // OK boots, start walking...
                    var t1 = Task.Run(
                        () => {
                            if (Services.TextTemplateReferences != null) {
                                IReadOnlyCollection<IUnresolvedReference> references = services.TextTemplateReferences.GetUnresolvedReferences();
                                AddUnresolvedReferences(collector, references);
                            }
                        });

                    var t2 = Task.Run(
                        () => {
                            if (Services.ProjectReferences != null) {
                                IReadOnlyCollection<IUnresolvedReference> references = services.ProjectReferences.GetUnresolvedReferences();
                                AddUnresolvedReferences(collector, references);
                            }
                        });

                    var t3 = Task.Run(
                        () => {
                            if (Services.AssemblyReferences != null) {
                                IReadOnlyCollection<IUnresolvedReference> references = services.AssemblyReferences.GetUnresolvedReferences();
                                AddUnresolvedReferences(collector, references);
                            }
                        });

                    return Task.WhenAll(t1, t2, t3);
                }
            } finally {
                BuildDependencyProjectReferencesService.ClearProjectReferenceGuidsInError();
            }

            return Task.CompletedTask;
        }

        private static void AddUnresolvedReferences(BuildDependenciesCollector collector, IReadOnlyCollection<IUnresolvedReference> references) {
            if (references != null && references.Count > 0) {
                collector.AddUnresolvedReferences(references);
            }
        }

        /// <summary>
        /// Analyzes the build dependencies.
        /// </summary>
        /// <param name="collector">The collector.</param>
        public void AnalyzeBuildDependencies(BuildDependenciesCollector collector) {
            // Force MEF import
            var services = Services;

            var aliasMap = collector.ExtensibilityImposition?.AliasMap;

            if (services.ProjectReferences != null) {
                var results = services.ProjectReferences.GetResolvedReferences(collector.UnresolvedReferences, null);
                if (results != null) {
                    foreach (var reference in results) {
                        AddResolvedDependency(reference.ExistingUnresolvedItem, reference.ResolvedReference);
                        collector.AddResolvedDependency(reference.ExistingUnresolvedItem, reference.ResolvedReference);
                    }
                }
            }

            if (services.AssemblyReferences != null) {
                AddResolvedAssemblyDependency(collector, services.AssemblyReferences.GetResolvedReferences(collector.UnresolvedReferences, aliasMap));
            }

            if (services.TextTemplateReferences != null) {
                AddResolvedAssemblyDependency(collector, services.TextTemplateReferences.GetResolvedReferences(collector.UnresolvedReferences, aliasMap));
            }
        }

        private void AddResolvedAssemblyDependency(BuildDependenciesCollector collector, IReadOnlyCollection<ResolvedDependency<IUnresolvedAssemblyReference, IAssemblyReference>> results) {
            if (results != null) {
                foreach (var resolvedDependency in results) {
                    AddResolvedAssemblyDependency(collector, resolvedDependency);
                }
            }
        }

        private void AddResolvedAssemblyDependency(BuildDependenciesCollector collector, ResolvedDependency<IUnresolvedAssemblyReference, IAssemblyReference> reference) {
            IResolvedDependency resolvedDependency = AddResolvedDependency(reference.ExistingUnresolvedItem, reference.ResolvedReference);
            AddTextTemplateDependency(reference, resolvedDependency);

            collector.AddResolvedDependency(reference.ExistingUnresolvedItem, reference.ResolvedReference);
        }

        private void AddTextTemplateDependency(ResolvedDependency<IUnresolvedAssemblyReference, IAssemblyReference> reference, IResolvedDependency resolvedDependency) {
            if (reference.ExistingUnresolvedItem.IsForTextTemplate) {
                if (textTemplateDependencies == null) {
                    textTemplateDependencies = new List<IResolvedDependency>();
                }

                if (textTemplateDependencies.All(r => r.ResolvedReference != resolvedDependency.ResolvedReference)) {
                    textTemplateDependencies.Add(resolvedDependency);
                }
            }
        }

        public void CalculateDirtyStateFromChanges(IList<ISourceChange> changes) {
            MarkThisFileDirty(changes);

            if (IsDirty && dirtyFiles != null) {
                return;
            }

            // check if this proj contains needed files
            List<ProjectItem> items = new List<ProjectItem>();

            items.AddRange(project.Value.GetItems("Compile"));
            items.AddRange(project.Value.GetItems("Content"));
            items.AddRange(project.Value.GetItems("None"));
            items.AddRange(project.Value.GetItems("XamlAppdef"));

            int abort = -1;

            foreach (var item in items) {
                if (abort > 0) {
                    break;
                }

                if (abort < 0 && changes.Count > 100) {
                    abort = 1;
                } else {
                    abort = 0;
                }

                for (var changeNum = changes.Count - 1; changeNum >= 0; changeNum--) {
                    var file = changes[changeNum];
                    string value = item.GetMetadataValue("FullPath");

                    if (string.Equals(value, file.FullPath, StringComparison.OrdinalIgnoreCase)) {
                        MarkDirty(BuildReasonTypes.ProjectItemChanged);

                        if (dirtyFiles == null) {
                            dirtyFiles = new List<string>();
                        }

                        dirtyFiles.Add(file.Path);

                        changes.RemoveAt(changeNum);

                        // If we have more than a few changes then abort early so we don't
                        // burn too much CPU when collecting this data. As long as we collect
                        // at least one match it's good enough.
                        if (abort > 0) {
                            break;
                        }
                    }
                }
            }
        }

        private void MarkDirty(BuildReasonTypes type) {
            IsDirty = true;
            this.SetReason(type);
        }

        private void MarkThisFileDirty(ICollection<ISourceChange> changes) {
            foreach (var change in changes) {
                if (string.Equals(FullPath, change.FullPath, StringComparison.OrdinalIgnoreCase)) {
                    MarkDirty(BuildReasonTypes.ProjectChanged);
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

        public IReadOnlyCollection<IResolvedDependency> GetTextTemplateDependencies() {
            return textTemplateDependencies;
        }

        public bool IsUnderSolutionRoot(string path) {
            return string.Equals(SolutionRoot, path, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal class BuildReason {
        public string Description { get; set; }
        public BuildReasonTypes Flags { get; set; }
        public IReadOnlyCollection<string> ChangedDependentProjects { get; set; }
    }

    internal static class BuildReasonExtensions {

        public static void SetReason(this ConfiguredProject project, BuildReasonTypes reasonTypes, string reasonDescription = null) {
            if (project.BuildReason == null) {
                project.BuildReason = new BuildReason { Flags = reasonTypes };
            } else {
                project.BuildReason.Flags |= reasonTypes;
            }

            if (reasonDescription != null) {
                project.BuildReason.Description = reasonDescription;
            }
        }
    }

}