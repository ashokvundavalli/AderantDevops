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
        private TrackedInputFilesController trackedInputFilesCheck;
        private Dictionary<string, InputFilesDependencyAnalysisResult> trackedInputs = new Dictionary<string, InputFilesDependencyAnalysisResult>(StringComparer.OrdinalIgnoreCase);

        [ImportingConstructor]
        public ProjectSequencer(ILogger logger, IFileSystem fileSystem) {
            this.logger = logger;
            this.fileSystem = fileSystem;

            this.trackedInputFilesCheck = new TrackedInputFilesController(fileSystem, logger);
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

            var projectGraph = new ProjectDependencyGraph(graph);
            // Ensure that any further usages of graph refer to our wrapper
            graph = projectGraph;

            AddInitializeAndCompletionNodes(context.Switches.ChangedFilesOnly, files.MakeFiles, projectGraph);

            var projectsInDependencyOrder = projectGraph.GetDependencyOrder();

            List<string> directoriesInBuild = new List<string>();

            foreach (IDependable dependable in projectsInDependencyOrder) {
                ConfiguredProject configuredProject = dependable as ConfiguredProject;

                if (configuredProject != null) {
                    if (assumeNoBuildCache) {
                        // No cache so mark everything as changed
                        configuredProject.IsDirty = true;
                        configuredProject.SetReason(BuildReasonTypes.Forced | BuildReasonTypes.CachedBuildNotFound);
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
                projectGraph,
                projectsInDependencyOrder,
                files,
                context.GetChangeConsiderationMode(),
                context.GetRelationshipProcessingMode());

            var groups = projectGraph.GetBuildGroups(filteredProjects);

            var treeText = PrintBuildTree(groups);
            if (logger != null) {
                logger.Info(treeText);
            }

            if (isDesktopBuild) {
                Thread.Sleep(2000);
            }

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
            var files = GetBuildStateFiles(context);

            if (files != null) {
                stateFiles = context.StateFiles = files;
            }
        }

        private List<BuildStateFile> GetBuildStateFiles(BuildOperationContext context) {
            List<BuildStateFile> files = new List<BuildStateFile>();

            // Here we select an appropriate tree to reuse
            var buildStateMetadata = context.BuildStateMetadata;

            if (buildStateMetadata != null && context.SourceTreeMetadata != null) {
                if (buildStateMetadata.BuildStateFiles != null) {
                    foreach (var bucketId in context.SourceTreeMetadata.GetBuckets()) {

                        BuildStateFile stateFile = null;

                        foreach (var file in buildStateMetadata.BuildStateFiles) {
                            if (string.Equals(file.BucketId.Id, bucketId.Id, StringComparison.OrdinalIgnoreCase)) {
                                stateFile = file;
                                break;
                            }
                        }

                        if (stateFile != null) {
                            logger.Info($"Using state file: {stateFile.Id} -> {stateFile.BuildId} -> {stateFile.Location}:{stateFile.BucketId.Tag}");
                            files.Add(stateFile);
                        }
                    }
                }
            }

            logger.Info($"Found {files.Count} state files.");
            return files;
        }

        private void AddInitializeAndCompletionNodes(bool changedFilesOnly, IReadOnlyCollection<string> makeFiles, ProjectDependencyGraph graph) {
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

            var grouping = graph.ProjectsBySolutionRoot;


            List<IArtifact> templateDependencyProviders = new List<IArtifact>();
            List<DirectoryNode> initializationNodes = new List<DirectoryNode>();

            foreach (var group in grouping) {
                List<ConfiguredProject> dirtyProjects = group.Where(g => g.IsDirty).ToList();

                string dirtyProjectsLogLine = string.Join(", ", dirtyProjects.Select(s => s.Id));

                string solutionDirectoryName = PathUtility.GetFileName(group.Key);

                IArtifact initializeNode;
                IArtifact completionNode;
                AddDirectoryNodes(graph, solutionDirectoryName, group.Key, out initializeNode, out completionNode);

                var stateFile = SelectStateFile(solutionDirectoryName);

                bool hasLoggedUpToDate = false;

                foreach (var project in projects.Where(p => p.IsUnderSolutionRoot(group.Key))) {
                    // The project depends on prolog file
                    project.AddResolvedDependency(null, initializeNode);

                    // The completion of the group depends all members of the group
                    completionNode.AddResolvedDependency(null, project);

                    // Push template dependencies into the prolog file to ensure it is scheduled after the dependencies are compiled
                    IReadOnlyCollection<IResolvedDependency> textTemplateDependencies = project.GetTextTemplateDependencies();
                    if (textTemplateDependencies != null) {
                        bool hasAddedNode = false;

                        foreach (var dependency in textTemplateDependencies) {
                            // Track that we need to add a transitive dependency later
                            if (!hasAddedNode) {
                                DirectoryNode directoryNode = initializeNode as DirectoryNode;
                                if (directoryNode != null && !initializationNodes.Contains(initializeNode)) {
                                    initializationNodes.Add(directoryNode);
                                    hasAddedNode = true;
                                }
                            }

                            initializeNode.AddResolvedDependency(dependency.ExistingUnresolvedItem, dependency.ResolvedReference);

                            IArtifact artifact = dependency.ResolvedReference as IArtifact;
                            // Store the item that produces this dependency for the second pass operation
                            if (artifact != null) {
                                if (!templateDependencyProviders.Contains(artifact)) {
                                    templateDependencyProviders.Add(artifact);
                                }
                            }
                        }
                    }

                    if (!changedFilesOnly) {
                        ApplyStateFile(stateFile, solutionDirectoryName, dirtyProjectsLogLine, project, ref hasLoggedUpToDate);
                    }
                }
            }

            AddTransitiveDependencies(graph, templateDependencyProviders, initializationNodes);
        }

        private static void AddTransitiveDependencies(ProjectDependencyGraph graph, List<IArtifact> templateDependencyProviders, List<DirectoryNode> initializationNodes) {
            // Now find all completion nodes of the artifacts that are involved in T4.
            // These will be added as transitive dependencies
            HashSet<DirectoryNode> producers = new HashSet<DirectoryNode>();

            foreach (var artifact in templateDependencyProviders) {
                foreach (DirectoryNode directoryNode in artifact.GetDependencies().OfType<DirectoryNode>()) {
                    foreach (IDependable graphNode in graph.Nodes) {
                        DirectoryNode node = graphNode as DirectoryNode;
                        if (node != null) {
                            if (node.IsPostTargets && string.Equals(node.Directory, directoryNode.Directory, StringComparison.OrdinalIgnoreCase)) {
                                producers.Add(node);
                            }
                        }
                    }
                }
            }

            foreach (var node in initializationNodes) {
                foreach (var producer in producers) {
                    // Do not add to ourselves because that would be circular
                    if (string.Equals(node.Directory, producer.Directory, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    // Janky hack to prevent cycles until we have a better way of abstracting this
                    if (producer.Directory.Contains("SoftwareFactory")) {
                        node.AddResolvedDependency(null, producer);
                    }
                }
            }
        }

        /// <summary>
        /// Synthesizes the nodes for all directories whether they contain a solution or not.
        /// We want to run any custom directory targets as these may invoke actions we cannot gain insight into and so need to
        /// schedule them as part of the sequence.
        /// </summary>
        private static void SynthesizeNodesForAllDirectories(IReadOnlyCollection<string> makeFiles, DependencyGraph graph) {
            if (makeFiles != null) {
                foreach (var makeFile in makeFiles) {
                    if (!string.IsNullOrWhiteSpace(makeFile)) {
                        var directoryAboveMakeFile = makeFile.Replace(@"Build\TFSBuild.proj", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd(Path.DirectorySeparatorChar);
                        string solutionDirectoryName = PathUtility.GetFileName(directoryAboveMakeFile);

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

        /// <summary>
        /// This is a reconciliation process. Applies the data from the state file to the current project.
        /// If the outputs look sane and safe then the project is removed from the build tree and the cached outputs are substituted in.
        /// </summary>
        internal void ApplyStateFile(BuildStateFile stateFile, string stateFileKey, string dirtyProjects, ConfiguredProject project, ref bool hasLoggedUpToDate) {
            string solutionRoot = project.SolutionRoot;
            InputFilesDependencyAnalysisResult result = BeginTrackingInputFiles(stateFile, solutionRoot);

            bool upToDate = result.IsUpToDate.GetValueOrDefault(true);
            bool hasTrackedFiles = result.TrackedFiles != null && result.TrackedFiles.Any();
            if (!upToDate && hasTrackedFiles) {
                MarkDirty("", project, BuildReasonTypes.InputsChanged);
                return;
            }

            if (upToDate && hasTrackedFiles && !hasLoggedUpToDate) {
                logger.Info("All tracked files are up to date: " + solutionRoot);
                hasLoggedUpToDate = true;
            }

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

                            MarkDirty(dirtyProjects, project, BuildReasonTypes.OutputNotFoundInCachedBuild);
                            return;
                        }
                    }
                } else {
                    logger.Info($"No artifacts exist for '{stateFileKey}' or there are no project outputs.");
                }

                MarkDirty(dirtyProjects, project, BuildReasonTypes.ProjectOutputNotFound);
                return;
            }

            MarkDirty(dirtyProjects, project, BuildReasonTypes.CachedBuildNotFound);
        }

        private InputFilesDependencyAnalysisResult BeginTrackingInputFiles(BuildStateFile stateFile, string solutionRoot) {
            if (!trackedInputs.ContainsKey(solutionRoot)) {
                var inputFilesAnalysisResult = trackedInputFilesCheck.PerformDependencyAnalysis(stateFile?.TrackedFiles, solutionRoot);

                if (inputFilesAnalysisResult != null) {
                    trackedInputs.Add(solutionRoot, inputFilesAnalysisResult);

                    if (PipelineService != null) {
                        if (inputFilesAnalysisResult.TrackedFiles != null && inputFilesAnalysisResult.TrackedFiles.Any()) {
                            var solutionRootName = PathUtility.GetFileName(solutionRoot);
                            logger.Info($"Tracking {inputFilesAnalysisResult.TrackedFiles.Count} input files for {solutionRootName}");

                            PipelineService.TrackInputFileDependencies(solutionRootName, inputFilesAnalysisResult.TrackedFiles);
                        }

                        return inputFilesAnalysisResult;
                    }
                }
            }

            return trackedInputs[solutionRoot];
        }

        private bool DoesArtifactContainProjectItem(ConfiguredProject project, ICollection<ArtifactManifest> artifacts) {
            // Packaged files such as workflows and web projects produce both an assembly an a package
            // We want to interrogate the package for the packaged content if we have one of those projects
            var outputFile = project.GetOutputAssemblyWithExtension();
            if (project.IsZipPackaged) {
                outputFile = Path.ChangeExtension(outputFile, ".zip");
            }

            List<ArtifactManifest> checkedArtifacts = null;

            foreach (ArtifactManifest artifactManifest in artifacts) {
                foreach (ArtifactItem file in artifactManifest.Files) {
                    if (string.Equals(file.File, outputFile, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }

                if (checkedArtifacts == null) {
                    checkedArtifacts = new List<ArtifactManifest>();
                }

                checkedArtifacts.Add(artifactManifest);
            }

            if (checkedArtifacts != null && checkedArtifacts.Count > 0) {
                logger.Info($"Looked for {outputFile} but it was not found in packages:");

                foreach (var checkedArtifact in checkedArtifacts) {
                    logger.Info(string.Format("    {0} (package id: {1})", checkedArtifact.Id.PadRight(80), checkedArtifact.InstanceId));
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

        private static void MarkDirty(string reasonDescription, ConfiguredProject project, BuildReasonTypes reasonTypes) {
            project.IsDirty = true;
            project.IncludeInBuild = true;
            project.SetReason(reasonTypes, reasonDescription);
        }

        /// <summary>
        /// According to options, find out which projects are selected to build.
        /// </summary>
        /// <param name="graph">The dependency graph</param>
        /// <param name="visualStudioProjects">All the projects list.</param>
        /// <param name="orchestrationFiles">File metadata for the target files that will orchestrate the build</param>
        /// <param name="changesToConsider">Build the current branch, the changed files since forking from master, or all?</param>
        /// <param name="dependencyProcessing">
        /// Build the directly affected downstream projects, or recursively search for all
        /// downstream projects, or none?
        /// </param>
        internal IReadOnlyList<IDependable> GetProjectsBuildList(
            ProjectDependencyGraph graph,
            IReadOnlyList<IDependable> visualStudioProjects,
            OrchestrationFiles orchestrationFiles,
            ChangesToConsider changesToConsider,
            DependencyRelationshipProcessing dependencyProcessing) {

            logger.Info("ChangesToConsider:" + changesToConsider);
            logger.Info("DependencyRelationshipProcessing:" + dependencyProcessing);

            var projects = visualStudioProjects.OfType<ConfiguredProject>().ToList();

            if (orchestrationFiles != null) {
                if (orchestrationFiles.ExtensibilityImposition != null) {
                    ApplyExtensibilityImposition(orchestrationFiles.ExtensibilityImposition, projects);
                }
            }

            // Get all the dirty projects due to user's modification.
            var dirtyProjects = visualStudioProjects.Where(p => IncludeProject(isDesktopBuild, p)).Select(x => x.Id).ToList();

            MarkWebProjectsDirty(graph);

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
            IReadOnlyList<IDependable> filteredProjects;
            if (changesToConsider == ChangesToConsider.None) {
                filteredProjects = visualStudioProjects;
            } else {
                filteredProjects = visualStudioProjects.Where(x => (x as ConfiguredProject)?.IsDirty != false).ToList();
            }

            return filteredProjects;
        }

        private void MarkWebProjectsDirty(ProjectDependencyGraph graph) {
            // TODO: Remove this hack as it costs too much perf
            // We need a way to trigger the content deployment of web projects

            ILookup<string, ConfiguredProject> lookup = graph.ProjectsBySolutionRoot;

            foreach (IGrouping<string, ConfiguredProject> grouping in lookup) {
                List<ConfiguredProject> configuredProjects = grouping.ToList();

                // A forced build is queued if any project within the directory container is also requiring a build
                List<ConfiguredProject> dirtyProjects = configuredProjects.Where(g => g.IsDirty).ToList();

                if (dirtyProjects.Count == 0) {
                    return;
                }

                bool buildWorkflowProjects = dirtyProjects.Any(x => x.IsTestProject);

                foreach (ConfiguredProject project in configuredProjects) {
                    if (project.IsWebProject && (buildWorkflowProjects || !project.IsWorkflowProject())) {
                        logger.Info($"Web project: '{project.FullPath}' requires forced build as: '{string.Join(", ", dirtyProjects)}' {(dirtyProjects.Count == 1 ? "is" : "are")} modified.");
                        // Web projects always need to be built as they have paths that reference content that needs to be deployed
                        // during the build.
                        // E.g script tags require that a file exists on disk. It's more reliable to have
                        // web pipeline restore this content for us than try and restore it as part of the generic build reuse process.
                        // <script type="text/javascript" src="../Scripts/ThirdParty.Jquery/jquery-2.2.4.js"></script>
                        MarkDirty("Dank workarounds", project, BuildReasonTypes.Forced);
                    }
                }
            }
        }

        private static bool IncludeProject(bool desktopBuild, IDependable x) {
            var configuredProject = x as ConfiguredProject;

            if (desktopBuild) {
                if (configuredProject != null) {
                    if (configuredProject.IsDirty && configuredProject.BuildReason.Flags == BuildReasonTypes.CachedBuildNotFound) {
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
                    };

                    if (configuredProject.DirtyFiles != null) {
                        children.Add(
                            new TreePrinter.Node {
                                Name = "Dirty files",
                                Children = configuredProject.DirtyFiles.Select(s => new TreePrinter.Node { Name = s }).ToList()
                            });
                    }

                    if (configuredProject.BuildReason != null) {
                        children.Add(new TreePrinter.Node { Name = "Flags: " + configuredProject.BuildReason.Flags });

                        if (configuredProject.BuildReason.ChangedDependentProjects != null)
                            children.Add(
                                new TreePrinter.Node {
                                    Name = "Changed Dependencies",
                                    Children = configuredProject.BuildReason.ChangedDependentProjects.Select(s => new TreePrinter.Node { Name = s }).ToList()
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
            List<IDependable> affectedProjects = new List<IDependable>();

            foreach (var project in allProjects) {
                ConfiguredProject configuredProject = project as ConfiguredProject;

                if (configuredProject != null) {
                    var dependencies = configuredProject.GetDependencies().Select(s => s.Id);

                    var intersect = dependencies.Intersect(projectsToFind, StringComparer.OrdinalIgnoreCase).ToList();

                    if (intersect.Any()) {
                        MarkDirty("", configuredProject, BuildReasonTypes.DependencyChanged);

                        configuredProject.BuildReason.ChangedDependentProjects = intersect;

                        affectedProjects.Add(configuredProject);
                    }
                }
            }

            return affectedProjects;
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

        internal static string PrintBuildTree(IReadOnlyList<IReadOnlyList<IDependable>> groups) {
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

            return sb.ToString();
        }
    }

    [Flags]
    internal enum BuildReasonTypes {
        None = 0,
        ProjectItemChanged = 1,
        CachedBuildNotFound = 2,
        OutputNotFoundInCachedBuild = 4,
        ProjectOutputNotFound = 8,
        DependencyChanged = 16,
        AlwaysBuild = 32,
        Forced = 64,
        InputsChanged = 128,
    }
}
