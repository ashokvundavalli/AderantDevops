using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Utilities;
using Aderant.Build.VersionControl.Model;

namespace Aderant.Build.DependencyAnalyzer {

    [Export(typeof(ISequencer))]
    internal class ProjectSequencer : ISequencer {
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;
        private bool isDesktopBuild;
        private List<BuildStateFile> stateFiles;

        [ImportingConstructor]
        public ProjectSequencer(ILogger logger, IFileSystem fileSystem) {
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        public IBuildPipelineService PipelineService { get; set; }

        /// <summary>
        /// Gets or sets the solution meta configuration.
        /// This is the SolutionConfiguration data from the sln.metaproj
        /// </summary>
        public string MetaprojectXml { get; set; }

        public BuildPlan CreatePlan(BuildOperationContext context, OrchestrationFiles files, DependencyGraph graph) {
            isDesktopBuild = context.IsDesktopBuild;

            bool assumeNoBuildCache = false;

            if (context.StateFiles == null) {
                FindStateFiles(context);

                if (stateFiles != null) {
                    EvictNotExistentProjects(context);
                }

                if (context.StateFiles == null || context.StateFiles.Count == 0) {
                    assumeNoBuildCache = true;
                }
            }

            AddInitializeAndCompletionNodes(context.Switches.ChangedFilesOnly, files.MakeFiles, graph);

            List<IDependable> projectsInDependencyOrder = graph.GetDependencyOrder();

            List<string> directoriesInBuild = new List<string>();

            foreach (IDependable dependable in projectsInDependencyOrder) {
                ConfiguredProject configuredProject = dependable as ConfiguredProject;

                if (configuredProject != null) {
                    if (assumeNoBuildCache) {
                        // No cache so mark everything as changed
                        configuredProject.IsDirty = true;
                        configuredProject.SetReason(BuildReasonTypes.Forced | BuildReasonTypes.BuildTreeNotFound);
                    }

                    if (!directoriesInBuild.Contains(configuredProject.SolutionRoot)) {
                        directoriesInBuild.Add(configuredProject.SolutionRoot);
                    }

                    if (PipelineService != null) {
                        PipelineService.TrackProject(
                            new OnDiskProjectInfo {
                                ProjectGuid = configuredProject.ProjectGuid,
                                SolutionRoot = configuredProject.SolutionRoot,
                                FullPath = configuredProject.FullPath,
                                OutputPath = configuredProject.OutputPath,
                                IsWebProject = configuredProject.IsWebProject,
                            });
                    }
                }
            }

            // According to options, find out which projects are selected to build.
            var filteredProjects = GetProjectsBuildList(
                projectsInDependencyOrder,
                files,
                context.GetChangeConsiderationMode(),
                context.GetRelationshipProcessingMode());

            List<List<IDependable>> groups = graph.GetBuildGroups(filteredProjects);

            PrintBuildTree(groups);

            var planGenerator = new BuildPlanGenerator(fileSystem);
            planGenerator.MetaprojectXml = MetaprojectXml;
            var project = planGenerator.GenerateProject(groups, files, null);

            return new BuildPlan(project) {
                DirectoriesInBuild = directoriesInBuild,
            };
        }

        private void EvictNotExistentProjects(BuildOperationContext context) {
            // here we evict deleted projects from the previous builds metadata
            // This is so we do not consider the outputs of this project in the artifact restore phase
            if (context.SourceTreeMetadata?.Changes != null) {
                IEnumerable<SourceChange> changes = context.SourceTreeMetadata.Changes;

                foreach (var sourceChange in changes) {
                    if (sourceChange.Status == FileStatus.Deleted) {

                        foreach (var file in stateFiles) {
                            if (file.Outputs.ContainsKey(sourceChange.Path)) {
                                file.Outputs.Remove(sourceChange.Path);
                            }
                        }
                    }
                }
            }
        }

        private void FindStateFiles(BuildOperationContext context) {
            var files = GetBuildStateFile(context);

            if (files != null) {
                stateFiles = context.StateFiles = files;
            }
        }

        private List<BuildStateFile> GetBuildStateFile(BuildOperationContext context) {
            List<BuildStateFile> files = new List<BuildStateFile>();

            // Here we select an appropriate tree to reuse
            var buildStateMetadata = context.BuildStateMetadata;

            if (buildStateMetadata != null && context.SourceTreeMetadata != null) {
                if (buildStateMetadata.BuildStateFiles != null) {
                    foreach (var bucketId in context.SourceTreeMetadata.GetBuckets()) {

                        BuildStateFile stateFile = null;

                        foreach (var file in buildStateMetadata.BuildStateFiles) {
                            if (string.Equals(file.BucketId.Id, bucketId.Id)) {
                                stateFile = file;
                                break;
                            }
                        }

                        if (stateFile != null) {
                            logger.Info($"Using state file: {stateFile.Id} -> {stateFile.BuildId} -> {stateFile.Location}:{stateFile.BucketId.Tag}.");
                            files.Add(stateFile);
                        }
                    }
                }
            }

            logger.Info($"Found {files.Count} state files.");
            return files;
        }

        private void AddInitializeAndCompletionNodes(bool changedFilesOnly, IReadOnlyCollection<string> makeFiles, DependencyGraph graph) {
            var projects = graph.Nodes
                .OfType<ConfiguredProject>()
                .ToList();

            if (changedFilesOnly) {
                foreach (var project in projects) {
                    if (!project.IsDirty) {
                        project.IncludeInBuild = false;
                    }
                }
            }

            SynthesizeNodesForAllDirectories(makeFiles, graph);

            var grouping = projects.GroupBy(g => Path.GetDirectoryName(g.SolutionFile), StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouping) {
                List<ConfiguredProject> dirtyProjects = group.Where(g => g.IsDirty).ToList();

                string tag = string.Join(", ", dirtyProjects.Select(s => s.Id));

                string solutionDirectoryName = Path.GetFileName(group.Key);

                IArtifact initializeNode;
                IArtifact completionNode;
                AddDirectoryNodes(graph, solutionDirectoryName, group.Key, out initializeNode, out completionNode);

                var stateFile = SelectStateFile(solutionDirectoryName);

                foreach (var project in projects.Where(p => string.Equals(Path.GetDirectoryName(p.SolutionFile), group.Key, StringComparison.OrdinalIgnoreCase))) {
                    // Push template dependencies into the prolog file to ensure it is scheduled after the dependencies are compiled
                    IReadOnlyCollection<IResolvedDependency> textTemplateDependencies = project.GetTextTemplateDependencies();
                    if (textTemplateDependencies != null) {
                        foreach (var dependency in textTemplateDependencies) {
                            initializeNode.AddResolvedDependency(dependency.ExistingUnresolvedItem, dependency.ResolvedReference);
                        }
                    }

                    if (!changedFilesOnly) {
                        ApplyStateFile(stateFile, solutionDirectoryName, tag, project);
                    }

                    project.AddResolvedDependency(null, initializeNode);
                    completionNode.AddResolvedDependency(null, project);
                }
            }
        }

        /// <summary>
        /// Synthesizes the nodes for all directories whether they contain a solution or not.
        /// We want to run any custom directory targets as these may invoke actions we cannot gain insight into and so need to schedule
        /// them as part of the sequence
        /// </summary>
        private static void SynthesizeNodesForAllDirectories(IReadOnlyCollection<string> makeFiles, DependencyGraph graph) {
            if (makeFiles != null) {
                foreach (var makeFile in makeFiles) {
                    if (!string.IsNullOrWhiteSpace(makeFile)) {
                        var directoryAboveMakeFile = makeFile.Replace(@"Build\TFSBuild.proj", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd(Path.DirectorySeparatorChar);
                        string solutionDirectoryName = Path.GetFileName(directoryAboveMakeFile);

                        IArtifact initializeNode;
                        IArtifact completionNode;
                        AddDirectoryNodes(graph, solutionDirectoryName, directoryAboveMakeFile, out initializeNode, out completionNode);
                    }
                }
            }
        }

        private static void AddDirectoryNodes(DependencyGraph graph, string nodeName, string nodeFullPath, out IArtifact initializeNode, out IArtifact completionNode) {
            // Create a new node that represents the start of a directory
            initializeNode = new DirectoryNode(nodeName, nodeFullPath, false);

            var existing = graph.GetNodeById(initializeNode.Id) as IArtifact;
            if (existing == null) {
                graph.Add(initializeNode);
            } else {
                initializeNode = existing;
            }

            // Create a new node that represents the completion of a directory
            completionNode = new DirectoryNode(nodeName, nodeFullPath, true);
            existing = graph.GetNodeById(completionNode.Id) as IArtifact;
            if (existing == null) {
                graph.Add(completionNode);
                completionNode.AddResolvedDependency(null, initializeNode);
            } else {
                completionNode = existing;
            }
        }

        private void ApplyStateFile(BuildStateFile stateFile, string stateFileKey, string tag, ConfiguredProject project) {
            // See if we can skip this project because we can re-use the previous outputs
            if (stateFile != null) {
                string projectFullPath = project.FullPath;

                bool artifactsExist = false;

                ICollection<ArtifactManifest> artifacts = null;
                if (stateFile.Artifacts != null) {
                    if (stateFile.Artifacts.TryGetValue(stateFileKey, out artifacts)) {
                        if (artifacts != null) {
                            artifactsExist = true;
                        }
                    }
                }

                if (artifactsExist && stateFile.Outputs != null) {
                    foreach (var projectInTree in stateFile.Outputs) {
                        // The selected build cache contained this project, next check the inputs/outputs
                        if (projectFullPath.IndexOf(projectInTree.Key, StringComparison.OrdinalIgnoreCase) >= 0) {

                            bool artifactContainsProject = DoesArtifactContainProjectItem(project, artifacts);
                            if (artifactContainsProject) {
                                return;
                            }

                            MarkDirty(tag, project, BuildReasonTypes.ArtifactsNotFound);
                            return;
                        }
                    }
                } else {
                    logger.Info($"No artifacts exist for: {stateFileKey} or there are no project outputs.");
                }

                MarkDirty(tag, project, BuildReasonTypes.ProjectOutputNotFound);
                return;
            }

            MarkDirty(tag, project, BuildReasonTypes.BuildTreeNotFound);
        }

        private bool DoesArtifactContainProjectItem(ConfiguredProject project, ICollection<ArtifactManifest> artifacts) {
            var outputFile = project.GetOutputAssemblyWithExtension();

            List<ArtifactManifest> checkedArtifacts = null;

            foreach (ArtifactManifest s in artifacts) {
                foreach (ArtifactItem file in s.Files) {
                    if (string.Equals(file.File, outputFile, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }

                if (checkedArtifacts == null) {
                    checkedArtifacts = new List<ArtifactManifest>();
                }

                checkedArtifacts.Add(s);
            }

            if (checkedArtifacts != null && checkedArtifacts.Count > 0) {
                logger.Info($"Looked for {outputFile} but it was not found in packages:");

                foreach (var checkedArtifact in checkedArtifacts) {
                    logger.Info(string.Format("    {0} ({1})", checkedArtifact.Id.PadRight(80), checkedArtifact.InstanceId));
                }
            }

            return false;
        }

        private BuildStateFile SelectStateFile(string stateFileKey) {
            foreach (var file in stateFiles) {
                if (string.Equals(file.BucketId.Tag, stateFileKey, StringComparison.OrdinalIgnoreCase)) {
                    return file;
                }
            }

            logger.Info($"No state files available for {stateFileKey}. Build will not be able to use prebuilt objects.");
            return null;
        }

        private static void MarkDirty(string tag, ConfiguredProject project, BuildReasonTypes reasonTypes) {
            project.IsDirty = true;
            project.IncludeInBuild = true;
            project.SetReason(reasonTypes, tag);
        }

        /// <summary>
        /// According to options, find out which projects are selected to build.
        /// </summary>
        /// <param name="visualStudioProjects">All the projects list.</param>
        /// <param name="orchestrationFiles"></param>
        /// <param name="changesToConsider">Build the current branch, the changed files since forking from master, or all?</param>
        /// <param name="dependencyProcessing">
        /// Build the directly affected downstream projects, or recursively search for all
        /// downstream projects, or none?
        /// </param>
        internal IReadOnlyCollection<IDependable> GetProjectsBuildList(IReadOnlyCollection<IDependable> visualStudioProjects, OrchestrationFiles orchestrationFiles, ChangesToConsider changesToConsider, DependencyRelationshipProcessing dependencyProcessing) {
            logger.Info("ChangesToConsider:" + changesToConsider);
            logger.Info("DependencyRelationshipProcessing:" + dependencyProcessing);

            var projects = visualStudioProjects.OfType<ConfiguredProject>().ToList();

            if (orchestrationFiles != null) {
                if (orchestrationFiles.ExtensibilityImposition != null) {
                    ApplyExtensibilityImposition(orchestrationFiles.ExtensibilityImposition, projects);
                }
            }

            ApplyQuirkFixes(projects);

            // Get all the dirty projects due to user's modification.
            var dirtyProjects = visualStudioProjects.Where(p => IncludeProject(isDesktopBuild, p)).Select(x => x.Id).ToList();

            HashSet<string> h = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            h.UnionWith(dirtyProjects);

            // According to DownStream option, either mark the directly affected or all the recursively affected downstream projects as dirty.
            switch (dependencyProcessing) {
                case DependencyRelationshipProcessing.Direct:
                    MarkDirty(visualStudioProjects, h);
                    break;
                case DependencyRelationshipProcessing.Transitive:
                    MarkDirtyAll(visualStudioProjects, h);
                    break;
            }

            // Get all projects that are either visualStudio projects and dirty, or not visualStudio projects. Or say, skipped the unchanged csproj projects.
            IReadOnlyCollection<IDependable> filteredProjects;
            if (changesToConsider == ChangesToConsider.None) {
                filteredProjects = visualStudioProjects;
            } else {
                filteredProjects = visualStudioProjects.Where(x => (x as ConfiguredProject)?.IsDirty != false).ToList();
            }

            return filteredProjects;
        }

        private void ApplyQuirkFixes(List<ConfiguredProject> projects) {
            foreach (var project in projects) {
                if (project.IsWebProject && !project.IsWorkflowProject()) {
                    // Web projects always need to be built as they have paths that reference content that needs to be deployed
                    // during the build.
                    // E.g tags such as this requires that we install this file on disk. It's more reliable to have
                    // web pipeline restore this content for us than try and restore it as part of the generic build reuse process.
                    // <script type="text/javascript" src="../Scripts/ThirdParty.Jquery/jquery-2.2.4.js"></script>
                    MarkDirty("", project, BuildReasonTypes.Forced);
                }
            }
        }

        private static bool IncludeProject(bool desktopBuild, IDependable x) {
            var configuredProject = x as ConfiguredProject;

            if (desktopBuild) {
                if (configuredProject != null) {
                    if (configuredProject.IsDirty && configuredProject.BuildReason.Flags.HasFlag(BuildReasonTypes.BuildTreeNotFound)) {
                        return false;
                    }

                    return configuredProject.IsDirty;
                }
            }

            return configuredProject?.IsDirty == true;
        }

        private void ApplyExtensibilityImposition(ExtensibilityImposition extensibilityImposition, List<ConfiguredProject> visualStudioProjects) {
            foreach (var file in extensibilityImposition.AlwaysBuildProjects) {
                var fileNameOfProject = Path.GetFileName(file);

                foreach (var project in visualStudioProjects) {
                    if (string.Equals(fileNameOfProject, Path.GetFileName(project.FullPath), StringComparison.OrdinalIgnoreCase)) {
                        project.IncludeInBuild = true;
                        project.IsDirty = true;
                        project.SetReason(BuildReasonTypes.AlwaysBuild);
                    }
                }
            }
        }

        private static List<TreePrinter.Node> DescribeChanges(IDependable dependable) {
            ConfiguredProject configuredProject = dependable as ConfiguredProject;

            if (configuredProject != null) {
                if (configuredProject.IncludeInBuild || configuredProject.IsDirty) {

                    var children = new List<TreePrinter.Node> {
                        new TreePrinter.Node { Name = "Path: " + configuredProject.FullPath, },
                        new TreePrinter.Node { Name = "Flags: " + configuredProject.BuildReason.Flags, }
                    };

                    if (configuredProject.DirtyFiles != null) {
                        children.Add(
                            new TreePrinter.Node() {
                                Name = "Dirty files",
                                Children = configuredProject.DirtyFiles.Select(s => new TreePrinter.Node { Name = s }).ToList()
                            });
                    }

                    return children;
                }
            }

            return null;
        }

        /// <summary>
        /// Mark all projects in allProjects where the project depends on any one in projectsToFind.
        /// </summary>
        /// <param name="allProjects">The full projects list.</param>
        /// <param name="projectsToFind">The project name hashset to search for.</param>
        /// <returns>The list of projects that gets dirty because they depend on any project found in the search list.</returns>
        internal List<IDependable> MarkDirty(IReadOnlyCollection<IDependable> allProjects, HashSet<string> projectsToFind) {
            var p = allProjects
                .Where(
                    x => (x as ConfiguredProject)?.GetDependencies()
                         .Select(y => y.Id)
                         .Intersect(projectsToFind).Any() == true).ToList();

            foreach (var x in p) {
                ConfiguredProject project = x as ConfiguredProject;
                if (project != null) {
                    MarkDirty("", project, BuildReasonTypes.DependencyChanged);
                }
            }

            return p;
        }

        /// <summary>
        /// Recursively mark all projects in allProjects where the project depends on any one in projectsToFind until no more
        /// parent project is found.
        /// </summary>
        internal void MarkDirtyAll(IReadOnlyCollection<IDependable> allProjects, HashSet<string> projectsToFind) {
            int newCount = -1;

            List<IDependable> p;
            while (newCount != 0) {
                p = MarkDirty(allProjects, projectsToFind).ToList();
                newCount = p.Count;
                var newSearchList = new HashSet<string>();
                newSearchList.UnionWith(p.Select(x => x.Id));
                projectsToFind = newSearchList;
            }
        }

        private void PrintBuildTree(List<List<IDependable>> groups) {
            if (logger != null) {
                var topLevelNodes = new List<TreePrinter.Node>();

                int i = 0;
                foreach (var group in groups) {
                    var topLevelNode = new TreePrinter.Node();
                    topLevelNode.Name = "Group " + i;
                    topLevelNode.Children = group.Select(
                        s =>
                            new TreePrinter.Node {
                                Name = s.Id,
                                Children = DescribeChanges(s)
                            }).ToList();
                    i++;

                    topLevelNodes.Add(topLevelNode);
                }

                StringBuilder sb = new StringBuilder();
                TreePrinter.Print(topLevelNodes, message => sb.Append(message));

                logger.Info(sb.ToString());

                if (isDesktopBuild) {
                    Thread.Sleep(2000);
                }
            }
        }
    }

    [Flags]
    internal enum BuildReasonTypes {
        None = 1,
        ProjectFileChanged = 2,
        BuildTreeNotFound = 4,
        ArtifactsNotFound = 8,
        ProjectOutputNotFound = 16,
        DependencyChanged = 32,
        AlwaysBuild = 64,
        Forced
    }
}
