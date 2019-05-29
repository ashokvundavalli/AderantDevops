using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Utilities;
using Aderant.Build.VersionControl;
using Aderant.Build.VersionControl.Model;

namespace Aderant.Build.DependencyAnalyzer {

    [Export(typeof(ISequencer))]
    internal class ProjectSequencer : ISequencer {
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;
        private bool isDesktopBuild;
        private List<BucketId> missingIds;
        private BuildCachePackageChecker packageChecker;
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


        public BuildPlan CreatePlan(BuildOperationContext context, OrchestrationFiles files, DependencyGraph graph, bool considerStateFiles = true) {
            isDesktopBuild = context.IsDesktopBuild;

            bool isBuildCacheEnabled = true;

            if (context.StateFiles == null) {
                FindStateFiles(context);

                if (stateFiles != null) {
                    EvictNotExistentProjects(context);
                }

                if (context.StateFiles == null || context.StateFiles.Count == 0) {
                    isBuildCacheEnabled = false;
                }
            }

            context.Variables["IsBuildCacheEnabled"] = isBuildCacheEnabled.ToString();

            var projectGraph = new ProjectDependencyGraph(graph);

            Sequence(context.Switches.ChangedFilesOnly, considerStateFiles, files.MakeFiles, projectGraph);

            var projectsInDependencyOrder = projectGraph.GetDependencyOrder();

            List<string> directoriesInBuild = new List<string>();
            TrackProjects(projectsInDependencyOrder, isBuildCacheEnabled, directoriesInBuild);

            // According to options, find out which projects are selected to build.
            var filteredProjects = GetProjectsBuildList(
                projectGraph,
                projectsInDependencyOrder,
                files,
                context.Switches.ExcludeTestProjects,
                context.GetChangeConsiderationMode(),
                context.GetRelationshipProcessingMode());

            FindAllChangedProjectsAndDisableBuildCache(projectGraph);

            filteredProjects = SecondPassAnalysis(filteredProjects, projectGraph);

            LogProjectsExcluded(filteredProjects, projectGraph);

            if (filteredProjects.Count != projectsInDependencyOrder.Count) {
                CacheSubstitutionFixup(projectsInDependencyOrder, filteredProjects);
            }

            LogPrebuiltStatus(filteredProjects);

            var groups = projectGraph.GetBuildGroups(filteredProjects);

            string treeText = PrintBuildTree(groups, true);
            if (logger != null) {
                logger.Info(treeText);

                WriteBuildTree(fileSystem, context.BuildRoot, treeText);
            }

            if (isDesktopBuild) {
                Thread.Sleep(2000);
            }

            var planGenerator = new BuildPlanGenerator(fileSystem);
            planGenerator.MetaprojectXml = MetaprojectXml;
            var project = planGenerator.GenerateProject(groups, files, isDesktopBuild, null);

            return new BuildPlan(project) {
                DirectoriesInBuild = directoriesInBuild,
            };
        }

        private void LogPrebuiltStatus(IReadOnlyList<IDependable> filteredProjects) {
            foreach (var project in filteredProjects.OfType<DirectoryNode>().Distinct()) {
                if (!project.IsPostTargets) {
                    logger.Info($"{project.DirectoryName} retrieve prebuilts: {(project.RetrievePrebuilts.HasValue ? project.RetrievePrebuilts.Value.ToString() : "?")}");
                }
            }
        }

        private void TrackProjects(IReadOnlyList<IDependable> projectsInDependencyOrder, bool isBuildCacheEnabled, List<string> directoriesInBuild) {
            foreach (IDependable dependable in projectsInDependencyOrder) {
                ConfiguredProject configuredProject = dependable as ConfiguredProject;

                if (configuredProject != null) {
                    if (isBuildCacheEnabled == false) {
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
        }

        /// <summary>
        /// If a project has no dependencies within the tree (nothing points to it) then it can be excluded from the tree during
        /// dependency analysis if the up-to-date check does not believe the project to be stale.
        /// If the directory has been marked as out-of-date then we need to propagate that state to all child projects to bring
        /// them back into the tree.
        /// Here we check for this and add the projects back in if the directory which owns them is invalidated.
        /// TODO: This should be handled as part of <see cref="GetProjectsBuildList" />
        /// </summary>
        internal IReadOnlyList<IDependable> SecondPassAnalysis(IReadOnlyList<IDependable> filteredProjects, ProjectDependencyGraph projectGraph) {
            var graph = new ProjectDependencyGraph();

            foreach (var project in projectGraph.Projects) {
                if (!project.IncludeInBuild) {
                    continue;
                }

                if (project.DirectoryNode.RetrievePrebuilts != null && project.DirectoryNode.RetrievePrebuilts.Value == false) {
                    if (project.BuildReason == null) {
                        project.SetReason(BuildReasonTypes.Forced, "SecondPassAnalysis");
                        graph.Add(project);
                    }
                }

                AddDependencyOnCacheDownload(projectGraph, project);
            }

            foreach (var filteredProject in filteredProjects) {
                if (filteredProject != null) {
                    graph.Add(filteredProject as IArtifact);
                } else {
                    throw new InvalidOperationException("Instance is not IArtifact");
                }
            }

            return graph.GetDependencyOrder().Where(
                p => {
                    var project = p as ConfiguredProject;

                    if (project != null) {
                        return project.BuildReason != null;
                    }

                    return true;
                }).ToList();
        }

        private static void AddDependencyOnCacheDownload(ProjectDependencyGraph projectGraph, ConfiguredProject project) {
            if (project.BuildReason != null /* Project is selected to build */) {
                IReadOnlyCollection<IDependable> dependencies = project.GetDependencies();
                foreach (IDependable dependency in dependencies) {
                    var dependentProject = projectGraph.GetNodeById<ConfiguredProject>(dependency.Id);

                    if (dependentProject != null) {
                        var directoryNode = dependentProject.DirectoryNode;

                        if (directoryNode != project.DirectoryNode) {
                            // Dependent project is not building - it must be coming from the cache.
                            // Therefore we need to simulate "TreatAsDependency" and take a concrete
                            // dependency on the cache download step
                            if (dependentProject.BuildReason == null) {
                                project.AddResolvedDependency(null, directoryNode);
                            }
                        }
                    }
                }
            }
        }

        private void LogProjectsExcluded(IReadOnlyList<IDependable> filteredProjects, ProjectDependencyGraph projectGraph) {
            var projectsExcluded = projectGraph.Nodes.Except(filteredProjects);

            StringBuilder sb = new StringBuilder();
            sb.Append("The following projects are excluded from this build:");
            sb.AppendLine();

            foreach (var project in projectsExcluded.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)) {
                ConfiguredProject configuredProject = projectGraph.GetProject(project.Id);
                if (configuredProject != null) {
                    sb.AppendLine(" -> " + configuredProject.Id);
                }
            }

            logger.Info(sb.ToString());
        }

        /// <summary>
        /// Fixes the dependency graph.
        /// <see cref="DependencyGraph.GetBuildGroups" /> will incorrectly group the graph when nodes are removed due to cache
        /// substitution or if a project is flagged to not build because not all are provided to GetBuildGroups
        /// (as they are not in the set returned by <see cref="GetProjectsBuildList" />.
        /// To fix this we add a synthetic dependency to on all nodes not building to all projects but only if the node itself has
        /// no items being built. This will introduce cycles but it doesn't matter as we have already sorted the graph but we just
        /// need to fix the grouping.
        /// </summary>
        private static void CacheSubstitutionFixup(IReadOnlyList<IDependable> projectsInDependencyOrder, IReadOnlyList<IDependable> filteredProjects) {
            foreach (var node in projectsInDependencyOrder.OfType<DirectoryNode>()) {
                if (!node.IsPostTargets && !node.IsBuildingAnyProjects) {
                    for (var i = 0; i < filteredProjects.Count; i++) {
                        IDependable dependable = filteredProjects[i];
                        ConfiguredProject artifact = dependable as ConfiguredProject;

                        if (artifact != null) {
                            artifact.AddResolvedDependency(null, node);
                        }
                    }
                }
            }
        }

        internal static void WriteBuildTree(IFileSystem fileSystem, string destination, string treeText) {
            string treeFile = Path.Combine(destination, "BuildTree.txt");

            if (fileSystem != null) {
                if (fileSystem.FileExists(treeFile)) {
                    fileSystem.DeleteFile(treeFile);
                }

                fileSystem.WriteAllText(treeFile, treeText);
            }
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
            IList<BuildStateFile> files = new List<BuildStateFile>();

            // Here we select an appropriate tree to reuse
            var buildStateMetadata = context.BuildStateMetadata;

            int bucketCount = -1;

            if (buildStateMetadata != null && context.SourceTreeMetadata != null) {
                if (buildStateMetadata.BuildStateFiles != null) {
                    IReadOnlyCollection<BucketId> buckets = context.SourceTreeMetadata.GetBuckets();

                    bucketCount = buckets.Count;

                    files = buildStateMetadata.QueryCacheForBuckets(buckets, out missingIds);

                    foreach (var stateFile in files) {
                        logger.Info($"Using state file: {stateFile.Id} -> {stateFile.BuildId} -> {stateFile.Location}:{stateFile.BucketId.Tag}");
                    }

                    foreach (var missingId in missingIds) {
                        logger.Info($"No state file: {missingId.Id} -> {missingId.Tag}");
                    }
                }

                logger.Info($"Found {files.Count}/{bucketCount} state files.");
            }

            return files.ToList();
        }

        private void MarkWebProjectsDirty(ProjectDependencyGraph graph) {
            // Web projects always need to be built as they have paths that reference content that needs to be deployed
            // during the build.
            // E.g script tags require that a file exists on disk. It's more reliable to have
            // web pipeline restore this content for us than try and restore it as part of the generic build reuse process.
            // Here a process optimization occurs, if we have a unit test project that is dirty we also dirty and related projects.
            // While this situation should not occur we want to avoid build failures if a test is scheduled to be built but any related
            // web projects are not.
            foreach (var item in graph.Projects) {
                if (!item.IsWorkflowProject() && item.IsWebProject && !item.IsDirty) {
                    IReadOnlyCollection<string> projectIds = graph.GetProjectsThatDirectlyDependOnThisProject(item.Id);

                    foreach (var id in projectIds) {
                        ConfiguredProject project = graph.GetProject(id);
                        if (project != null) {
                            if (project.IsWebProject && !project.IsDirty) {
                                MarkDirty(project, BuildReasonTypes.Forced, "MarkWebProjectsDirty");
                            }
                        }
                    }
                }
            }
        }

        public void Sequence(bool changedFilesOnly, bool considerStateFiles, IReadOnlyCollection<string> makeFiles, ProjectDependencyGraph graph) {
            var projects = graph.Projects;

            if (changedFilesOnly) {
                foreach (var project in projects) {
                    if (!project.IsDirty) {
                        project.IncludeInBuild = false;
                    }
                }
            }

            var startNodes = SynthesizeNodesForAllDirectories(makeFiles, graph);

            var grouping = graph.ProjectsBySolutionRoot;

            foreach (var group in grouping) {
                List<ConfiguredProject> dirtyProjects = group.Where(g => g.IsDirty).ToList();

                var dirtyProjectsLogLine = dirtyProjects.Select(s => s.Id);

                string solutionDirectoryName = PathUtility.GetFileName(group.Key ?? "");

                DirectoryNode startNode;
                DirectoryNode endNode;
                AddDirectoryNodes(graph, solutionDirectoryName, group.Key, out startNode, out endNode);

                BuildStateFile stateFile = null;
                if (considerStateFiles) {
                    stateFile = SelectStateFile(solutionDirectoryName);
                }

                bool hasLoggedUpToDate = false;

                foreach (var project in projects.Where(p => p.IsUnderSolutionRoot(group.Key))) {
                    // The project depends on prolog file
                    project.AddResolvedDependency(null, startNode);
                    project.DirectoryNode = startNode;

                    // The completion of the group depends all members of the group
                    endNode.AddResolvedDependency(null, project);

                    // Push template dependencies into the prolog file to ensure it is scheduled after the dependencies are compiled
                    IReadOnlyCollection<IResolvedDependency> textTemplateDependencies = project.GetTextTemplateDependencies();
                    if (textTemplateDependencies != null) {
                        bool hasAddedNode = false;

                        foreach (var dependency in textTemplateDependencies) {
                            if (!hasAddedNode) {
                                DirectoryNode directoryNode = startNode;
                                if (directoryNode != null && !startNodes.Contains(startNode)) {
                                    startNodes.Add(directoryNode);
                                    hasAddedNode = true;
                                }
                            }

                            startNode.AddResolvedDependency(dependency.ExistingUnresolvedItem, dependency.ResolvedReference);
                        }
                    }

                    if (!changedFilesOnly) {
                        if (considerStateFiles) {
                            ApplyStateFile(stateFile, solutionDirectoryName, dirtyProjectsLogLine, project, ref hasLoggedUpToDate);

                            if (project.IsDirty) {
                                startNode.IsBuildingAnyProjects = true;
                            }
                        }
                    }
                }
            }

            graph.ComputeReverseReferencesMap();
        }

        private static void FindAllChangedProjectsAndDisableBuildCache(ProjectDependencyGraph graph) {
            var projects = graph.Projects;

            var items = projects.Where(
                project => project.BuildReason != null
                           && (project.BuildReason.Flags.HasFlag(BuildReasonTypes.DependencyChanged) || project.BuildReason.Flags.HasFlag(BuildReasonTypes.InputsChanged)));

            foreach (var item in items) {
                if (item.DirectoryNode != null) {
                    if (item.BuildReason.Flags.HasFlag(BuildReasonTypes.ProjectOutputNotFound)) {
                        continue;
                    }
                    item.DirectoryNode.RetrievePrebuilts = false;
                }
            }
        }

        /// <summary>
        /// Synthesizes the nodes for all directories whether they contain a solution or not.
        /// We want to run any custom directory targets as these may invoke actions we cannot gain insight into and so need to
        /// schedule them as part of the sequence.
        /// </summary>
        internal List<DirectoryNode> SynthesizeNodesForAllDirectories(IReadOnlyCollection<string> makeFiles, DependencyGraph graph) {
            var list = new List<string>(makeFiles ?? Enumerable.Empty<string>());

            IReadOnlyCollection<BuildDirectoryContribution> contributions = null;
            if (PipelineService != null) {
                contributions = PipelineService.GetContributors();
                foreach (var contribution in contributions) {
                    if (!list.Contains(contribution.File, StringComparer.OrdinalIgnoreCase)) {
                        list.Add(contribution.File);
                    }
                }
            }

            List<DirectoryNode> startNodes = new List<DirectoryNode>();
            List<DirectoryNode> endNodes = new List<DirectoryNode>();

            Dictionary<string, DependencyManifest> manifestMap = new Dictionary<string, DependencyManifest>(StringComparer.OrdinalIgnoreCase);

            foreach (var makeFile in list) {
                if (!string.IsNullOrWhiteSpace(makeFile)) {
                    if (fileSystem.FileExists(makeFile)) {
                        var directoryAboveMakeFile = makeFile.Replace(WellKnownPaths.EntryPointFilePath, string.Empty, StringComparison.OrdinalIgnoreCase).TrimTrailingSlashes();
                        string nodeName = PathUtility.GetFileName(directoryAboveMakeFile);

                        DirectoryNode startNode;
                        DirectoryNode endNode;
                        AddDirectoryNodes(graph, nodeName, directoryAboveMakeFile, out startNode, out endNode);

                        startNodes.Add(startNode);
                        endNodes.Add(endNode);

                        // Check if this item was added by the user or by the system. This information is used later to determine if
                        // we need to run user steps (like solution packaging etc) or if these can be skipped
                        if (contributions != null) {
                            var contribution = contributions.FirstOrDefault(s => string.Equals(s.File, makeFile, StringComparison.OrdinalIgnoreCase));
                            if (contribution != null) {

                                // Ensure the user did not explicitly ask this directory to be built
                                if (!makeFiles.Contains(contribution.File)) {
                                    startNode.AddedByDependencyAnalysis = true;
                                    endNode.AddedByDependencyAnalysis = true;

                                    if (contribution.DependencyManifest != null) {
                                        manifestMap[contribution.DependencyFile] = contribution.DependencyManifest;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            SetupDirectoryDependencies(startNodes, endNodes, manifestMap);

            return startNodes;
        }

        private void SetupDirectoryDependencies(List<DirectoryNode> startNodes, List<DirectoryNode> endNodes, Dictionary<string, DependencyManifest> manifestMap) {
            foreach (var node in startNodes) {
                string manifestFilePath = Path.Combine(node.Directory, "Build", DependencyManifest.DependencyManifestFileName);

                DependencyManifest manifest;
                if (!manifestMap.TryGetValue(manifestFilePath, out manifest)) {
                    if (fileSystem.FileExists(manifestFilePath)) {
                        manifest = new DependencyManifest("", XDocument.Parse(fileSystem.ReadAllText(manifestFilePath)));
                    }
                }

                if (manifest == null) {
                    continue;
                }

                foreach (var item in manifest.ReferencedModules) {
                    var nameToTreatAsDependency = item.CustomAttributes
                        .FirstOrDefault(s => string.Equals(s.Name.LocalName, "TreatAsDependency", StringComparison.OrdinalIgnoreCase) && string.Equals(s.Value, "true", StringComparison.OrdinalIgnoreCase));

                    if (nameToTreatAsDependency != null) {
                        DirectoryNode dependency = endNodes.FirstOrDefault(s => string.Equals(s.DirectoryName, item.Name, StringComparison.OrdinalIgnoreCase) && s.IsPostTargets);

                        if (dependency != null) {
                            node.AddResolvedDependency(null, dependency);
                        }
                    }
                }
            }
        }

        private static void AddDirectoryNodes(DependencyGraph graph, string nodeName, string nodeFullPath, out DirectoryNode startNode, out DirectoryNode endNode) {
            // Create a new node that represents the start of a directory
            startNode = new DirectoryNode(nodeName, nodeFullPath, false);

            var existing = graph.GetNodeById<DirectoryNode>(startNode.Id);
            if (existing == null) {
                graph.Add(startNode);
            } else {
                startNode = existing;
            }

            // Create a new node that represents the completion of a directory
            endNode = new DirectoryNode(nodeName, nodeFullPath, true);
            existing = graph.GetNodeById<DirectoryNode>(endNode.Id);
            if (existing == null) {
                graph.Add(endNode);
                endNode.AddResolvedDependency(null, startNode);
            } else {
                endNode = existing;
            }
        }

        /// <summary>
        /// This is a reconciliation process. Applies the data from the state file to the current project.
        /// If the outputs look sane and safe then the project is removed from the build tree and the cached outputs are
        /// substituted in.
        /// </summary>
        internal void ApplyStateFile(BuildStateFile stateFile, string stateFileKey, IEnumerable<string> dirtyProjects, ConfiguredProject project, ref bool hasLoggedUpToDate) {
            string solutionRoot = project.SolutionRoot;
            InputFilesDependencyAnalysisResult result = BeginTrackingInputFiles(stateFile, solutionRoot);

            bool upToDate = result.IsUpToDate.GetValueOrDefault(true);
            bool hasTrackedFiles = result.TrackedFiles != null && result.TrackedFiles.Any();
            if (!upToDate && hasTrackedFiles) {
                MarkDirty(project, BuildReasonTypes.InputsChanged, "");
                return;
            }

            if (upToDate && hasTrackedFiles && !hasLoggedUpToDate) {
                logger.Info("All tracked files are up to date: " + solutionRoot);
                hasLoggedUpToDate = true;
            }

            // See if we can skip this project because we can re-use the previous outputs
            if (stateFile != null) {
                if (this.packageChecker == null) {
                    this.packageChecker = new BuildCachePackageChecker(logger);
                }

                packageChecker.Artifacts = null;

                bool artifactsExist = false;

                ICollection<ArtifactManifest> artifacts = null;
                if (stateFile.Artifacts != null) {
                    if (stateFile.Artifacts.TryGetValue(stateFileKey, out artifacts)) {
                        if (artifacts != null) {
                            artifactsExist = true;
                        }
                    }
                }

                string projectFullPath = project.FullPath;

                if (artifactsExist && stateFile.Outputs != null) {
                    packageChecker.Artifacts = artifacts;

                    foreach (var projectInTree in stateFile.Outputs) {
                        // Combine the original directory and the relative path to the project. This is done to get a unique path segment.
                        // This avoids false selection of projects where "A\B.proj" may exist across two directories.
                        string projectPath = Path.Combine(projectInTree.Value.Directory, projectInTree.Key);

                        if (projectFullPath.IndexOf(projectPath, StringComparison.OrdinalIgnoreCase) >= 0) {
                            // The selected build cache contained this project, next check the inputs/outputs

                            bool artifactContainsProject = packageChecker.DoesArtifactContainProjectItem(project);
                            if (artifactContainsProject) {
                                return;
                            }

                            MarkDirty(project, BuildReasonTypes.OutputNotFoundInCachedBuild, (string)null);
                            return;
                        }
                    }
                } else {
                    logger.Info($"No artifacts exist for '{stateFileKey}' or there are no project outputs.");
                }

                MarkDirty(project, BuildReasonTypes.ProjectOutputNotFound, (string)null);
                return;

            }

            MarkDirty(project, BuildReasonTypes.CachedBuildNotFound, (string)null);
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


        private BuildStateFile SelectStateFile(string stateFileKey) {
            foreach (var file in stateFiles) {
                if (string.Equals(file.BucketId.Tag, stateFileKey, StringComparison.OrdinalIgnoreCase)) {
                    return file;
                }
            }

            logger.Info($"No state files available for {stateFileKey}. Build will not be able to use prebuilt objects.");
            return null;
        }

        private static void MarkDirty(ConfiguredProject project, BuildReasonTypes reasonTypes, string reasonDescription) {
            project.IsDirty = true;
            project.IncludeInBuild = true;
            project.SetReason(reasonTypes, reasonDescription);
        }

        private static void MarkDirty(ConfiguredProject project, BuildReasonTypes reasonTypes, IEnumerable<string> changedProjects) {
            MarkDirty(project, reasonTypes, (string)null);

            if (changedProjects != null) {
                if (project.BuildReason.ChangedDependentProjects == null || project.BuildReason.ChangedDependentProjects.Count == 0) {
                    project.BuildReason.ChangedDependentProjects = changedProjects.ToArray();
                }
            }
        }

        /// <summary>
        /// According to options, find out which projects are selected to build.
        /// </summary>
        /// <param name="studioProjects"></param>
        /// <param name="buildableProjects">All the projects list.</param>
        /// <param name="orchestrationFiles">File metadata for the target files that will orchestrate the build</param>
        /// <param name="excludeTestProjects">Specifies if test projects be removed from the build tree.</param>
        /// <param name="changesToConsider">Build the current branch, the changed files since forking from master, or all?</param>
        /// <param name="dependencyProcessing">
        /// Build the directly affected downstream projects, or recursively search for all
        /// downstream projects, or none?
        /// </param>
        internal IReadOnlyList<IDependable> GetProjectsBuildList(
            ProjectDependencyGraph studioProjects,
            IReadOnlyList<IDependable> buildableProjects,
            OrchestrationFiles orchestrationFiles,
            bool excludeTestProjects,
            ChangesToConsider changesToConsider,
            DependencyRelationshipProcessing dependencyProcessing) {

            logger.Info("ChangesToConsider:" + changesToConsider);
            logger.Info("DependencyRelationshipProcessing:" + dependencyProcessing);

            var projects = buildableProjects.OfType<ConfiguredProject>().ToList();

            bool alwaysBuildWebProjects = true;

            if (orchestrationFiles != null) {
                if (orchestrationFiles.ExtensibilityImposition != null) {
                    ApplyExtensibilityImposition(orchestrationFiles.ExtensibilityImposition, projects);

                    alwaysBuildWebProjects = orchestrationFiles.ExtensibilityImposition.AlwaysBuildWebProjects;
                }
            }

            // Get all the dirty projects due to user's modification.
            var dirtyProjects = buildableProjects.Where(p => IncludeProject(excludeTestProjects, p)).Select(x => x.Id).ToList();

            if (alwaysBuildWebProjects) {
                MarkWebProjectsDirty(studioProjects);
            }

            HashSet<string> h = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            h.UnionWith(dirtyProjects);

            // According to DownStream option, either mark the directly affected or all the recursively affected downstream projects as dirty.
            switch (dependencyProcessing) {
                case DependencyRelationshipProcessing.Direct:
                    MarkDirty(buildableProjects, h);
                    break;
                case DependencyRelationshipProcessing.Transitive:
                    MarkDirtyAll(buildableProjects, h);
                    break;
            }

            // Get all projects that are either visualStudio projects and dirty, or not visualStudio projects. Or say, skipped the unchanged csproj projects.
            IReadOnlyList<IDependable> filteredProjects;
            if (changesToConsider == ChangesToConsider.None) {
                filteredProjects = buildableProjects;
            } else {
                filteredProjects = buildableProjects.Where(x => !(x is ConfiguredProject) || ((ConfiguredProject)x).IsDirty).ToList();
            }

            return filteredProjects;
        }

        private static bool IncludeProject(bool excludeTestProjects, IDependable x) {
            var configuredProject = x as ConfiguredProject;

            if (configuredProject != null) {
                if (!configuredProject.IncludeInBuild) {
                    return false;
                }

                if (excludeTestProjects) {
                    // Web projects are also test projects so don't be too aggressive here.
                    if (configuredProject.IsTestProject && !configuredProject.IsWebProject) {
                        return false;
                    }
                }

                if (configuredProject.DirtyFiles != null && configuredProject.DirtyFiles.Count > 0) {
                    return true;
                }

            }

            if (configuredProject != null) {
                var reason = configuredProject.BuildReason;

                return configuredProject.IsDirty &&
                        !reason.Flags.HasFlag(BuildReasonTypes.CachedBuildNotFound) && !reason.Flags.HasFlag(BuildReasonTypes.AlwaysBuild);
            }

            return false;
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

        private static List<TreePrinter.Node> DescribeChanges(IDependable dependable, bool showPath) {
            ConfiguredProject configuredProject = dependable as ConfiguredProject;

            if (configuredProject != null) {
                if (configuredProject.IncludeInBuild || configuredProject.IsDirty) {

                    var children = new List<TreePrinter.Node>();
                    if (showPath) {
                        children.Add(new TreePrinter.Node { Name = "Path: " + configuredProject.FullPath, });
                    }

                    if (configuredProject.DirtyFiles != null) {
                        children.Add(
                            new TreePrinter.Node {
                                Name = "Dirty files",
                                Children = configuredProject.DirtyFiles.Select(s => new TreePrinter.Node { Name = s }).ToList()
                            });
                    }

                    if (configuredProject.BuildReason != null) {

                        string reason = string.Empty;
                        if (!string.IsNullOrWhiteSpace(configuredProject.BuildReason.Description)) {
                            reason = "Reason: " + configuredProject.BuildReason.Description;
                        }

                        children.Add(new TreePrinter.Node { Name = string.Format("Flags: {0}. {1}", configuredProject.BuildReason.Flags, reason) });

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
                        MarkDirty(configuredProject, BuildReasonTypes.DependencyChanged, "");

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
                var newSearchList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                newSearchList.UnionWith(p.Select(x => x.Id));
                projectsToFind = newSearchList;
            }
        }

        internal static string PrintBuildTree(IReadOnlyList<IReadOnlyList<IDependable>> groups, bool showPath) {
            var topLevelNodes = new List<TreePrinter.Node>();

            int i = 0;
            foreach (var group in groups) {
                var topLevelNode = new TreePrinter.Node();
                topLevelNode.Name = "Group " + i;
                topLevelNode.Children = group.Select(
                    s =>
                        new TreePrinter.Node {
                            Name = s.Id.Split(':').Last(),
                            Children = DescribeChanges(s, showPath)
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
        ProjectChanged = 256,
    }
}