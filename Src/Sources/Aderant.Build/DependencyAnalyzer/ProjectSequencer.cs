using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Utilities;

namespace Aderant.Build.DependencyAnalyzer {
    [Export(typeof(ISequencer))]
    internal class ProjectSequencer : ISequencer {
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;
        private DirectoryNodeFactory directoryNodeFactory;
        internal BuildCachePackageChecker PackageChecker;
        private List<BuildStateFile> stateFiles;
        private TrackedInputFilesController trackedInputFilesCheck;
        private Dictionary<string, InputFilesDependencyAnalysisResult> trackedInputs = new Dictionary<string, InputFilesDependencyAnalysisResult>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A reasonable starting pint for lists that will hold directory entries. Source trees typically contain many folders
        /// so we wan to have a good default capacity.
        /// </summary>
        private const int DefaultDirectoryListCapacity = ListHelper.DefaultDirectoryListCapacity;

        [ImportingConstructor]
        public ProjectSequencer(ILogger logger, IFileSystem fileSystem) {
            this.logger = logger;
            this.fileSystem = fileSystem;

            this.trackedInputFilesCheck = new TrackedInputFilesController(fileSystem, logger);
        }

        internal TrackedInputFilesController TrackedInputFilesCheck {
            get { return trackedInputFilesCheck; }
            set { trackedInputFilesCheck = value; }
        }

        public IBuildPipelineService PipelineService { get; set; }

        /// <summary>
        /// Gets or sets the solution meta configuration.
        /// This is the SolutionConfiguration data from the sln.metaproj
        /// </summary>
        public string MetaprojectXml { get; set; }

        public BuildPlan CreatePlan(BuildOperationContext context, OrchestrationFiles files, DependencyGraph graph, bool considerStateFiles) {
            if (context.StateFiles == null) {
                var manager = new StateFileController();
                stateFiles = manager.GetApplicableStateFiles(logger, context);
            } else {
                stateFiles = context.StateFiles;
            }

            var projectGraph = new ProjectDependencyGraph(graph);

            List<BuildStateFile> buildStateFiles = Sequence(context.Switches, considerStateFiles, files.MakeFiles, projectGraph, context.BuildMetadata);

            if (buildStateFiles.Any()) {
                // Configure state files based on filtered output.
                context.StateFiles = buildStateFiles;
            }

            var isBuildCacheEnabled = StateFileController.SetIsBuildCacheEnabled(buildStateFiles, context);

            var projectsInDependencyOrder = projectGraph.GetDependencyOrder();

            List<string> directoriesInBuild = new List<string>(DefaultDirectoryListCapacity);
            TrackProjects(projectsInDependencyOrder, isBuildCacheEnabled, directoriesInBuild);

            // According to options, find out which projects are selected to build.
            var filteredProjects = GetProjectsBuildList(
                projectGraph,
                projectsInDependencyOrder,
                files,
                context.Switches.ExcludeTestProjects,
                context.GetChangeConsiderationMode(),
                context.GetRelationshipProcessingMode());

            var option = files.ExtensibilityImposition.BuildCacheOptions;

            filteredProjects = SecondPassAnalysis(filteredProjects, projectGraph, option);

            LogProjectsExcluded(filteredProjects, projectGraph);

            LogPrebuiltStatus(filteredProjects);

            var groups = DependencyGraph.GetBuildGroups(filteredProjects);

            LogTree(context, groups);

            var planGenerator = new BuildPlanGenerator(fileSystem);
            planGenerator.MetaprojectXml = MetaprojectXml;
            var project = planGenerator.GenerateProject(groups, files, context.IsDesktopBuild, null);

            return new BuildPlan(project) {
                DirectoriesInBuild = directoriesInBuild,
            };
        }

        private void LogTree(BuildOperationContext context, IReadOnlyList<IReadOnlyList<IDependable>> groups) {
            string treeText = PrintBuildTree(groups, true);
            logger.Info(treeText, null);

            WriteBuildTree(fileSystem, context.BuildRoot, treeText);

            WaitForUserToReviewTree(context);
        }

        private static void WaitForUserToReviewTree(BuildOperationContext context) {
            if (GiveTimeToReviewTree) {
                if (context.IsDesktopBuild) {
                    Thread.Sleep(2000);
                }
            }
        }

        /// <summary>
        /// Instructs the sequencer to not pause when dumping the build tree to the current logger.
        /// </summary>
        internal static bool GiveTimeToReviewTree { get; set; } = true;

        private void LogPrebuiltStatus(IReadOnlyList<IDependable> filteredProjects) {
            foreach (var project in filteredProjects.OfType<DirectoryNode>().Distinct()) {
                if (!project.IsPostTargets) {
                    logger.Info($"{project.DirectoryName} retrieve prebuilts: {(project.RetrievePrebuilts.HasValue ? project.RetrievePrebuilts.Value.ToString() : "?")}", null);
                }
            }
        }

        private void TrackProjects(IReadOnlyList<IDependable> projectsInDependencyOrder, bool isBuildCacheEnabled, List<string> directoriesInBuild) {
            foreach (IDependable dependable in projectsInDependencyOrder) {
                ConfiguredProject configuredProject = dependable as ConfiguredProject;

                if (configuredProject != null) {
                    if (isBuildCacheEnabled == false) {
                        // No cache so mark everything as changed.
                        configuredProject.MarkDirtyAndSetReason(BuildReasonTypes.Forced | BuildReasonTypes.CachedBuildNotFound);
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
        internal IReadOnlyList<IDependable> SecondPassAnalysis(IReadOnlyList<IDependable> filteredProjects, ProjectDependencyGraph projectGraph, BuildCacheOptions option) {
            var graph = new ProjectDependencyGraph();

            bool disableBuildCache = option == BuildCacheOptions.DisableCacheWhenProjectChanged;

            if (disableBuildCache) {
                DisableBuildCache(projectGraph.Projects);
            }

            foreach (var project in projectGraph.Projects) {
                if (!project.IncludeInBuild) {
                    continue;
                }

                if (project.IsDirty) {
                    project.DirectoryNode.IsBuildingAnyProjects = true;
                }

                if (disableBuildCache) {
                    if (project.DirectoryNode.RetrievePrebuilts != null && project.DirectoryNode.RetrievePrebuilts.Value == false) {
                        if (project.BuildReason == null || !project.IsDirty) {
                            project.MarkDirtyAndSetReason(BuildReasonTypes.Forced, "SecondPassAnalysis");
                        } else {
                            if (!project.IsDirty && project.BuildReason?.Flags == BuildReasonTypes.RequiredByTextTemplate) {
                                project.IsDirty = true;
                            }
                        }

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

            logger.Info(sb.ToString(), null);
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

        public List<BuildStateFile> Sequence(BuildSwitches switches, bool considerStateFiles, IReadOnlyCollection<string> makeFiles, ProjectDependencyGraph graph, BuildMetadata buildMetadata) {
            var projects = graph.Projects;

            if (switches.ChangedFilesOnly) {
                foreach (var project in projects) {
                    if (!project.IsDirty) {
                        project.IncludeInBuild = false;
                    }
                }
            }

            var startNodes = SynthesizeNodesForAllDirectories(makeFiles, graph);

            var grouping = graph.ProjectsBySolutionRoot;

            List<BuildStateFile> buildStateFiles = new List<BuildStateFile>(64);

            foreach (var group in grouping) {
                string solutionDirectoryName = PathUtility.GetFileName(group.Key ?? string.Empty);

                DirectoryNode startNode;
                DirectoryNode endNode;
                AddDirectoryNodes(graph, solutionDirectoryName, group.Key, out startNode, out endNode);

                BuildStateFile[] selectedStateFiles = null;
                if (considerStateFiles) {
                    selectedStateFiles = SelectStateFiles(solutionDirectoryName);
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

                            // Configure the project to be dirty but not built to ensure correct ordering.
                            ConfiguredProject configuredProject = dependency.ResolvedReference as ConfiguredProject;
                            if (configuredProject != null) {
                                if (!configuredProject.IsDirty) {
                                    configuredProject.MarkDirtyAndSetReason(BuildReasonTypes.RequiredByTextTemplate);
                                    configuredProject.IsDirty = false;
                                }
                            }

                            startNode.AddResolvedDependency(dependency.ExistingUnresolvedItem, dependency.ResolvedReference);
                        }
                    }

                    if (!switches.ChangedFilesOnly) {
                        if (considerStateFiles) {
                            BuildStateFile stateFile = ApplyStateFile(selectedStateFiles, solutionDirectoryName, project, switches.SkipNugetPackageHashCheck, ref hasLoggedUpToDate, buildMetadata);

                            if (stateFile != null) {
                                // Add the new file if it isn't already in the list
                                if (!buildStateFiles.Contains(stateFile)) {
                                    buildStateFiles.Add(stateFile);
                                }
                            }

                            if (project.IsDirty) {
                                startNode.IsBuildingAnyProjects = true;
                            }
                        }
                    }
                }
            }

            graph.ComputeReverseReferencesMap();

            return buildStateFiles;
        }

        private static void DisableBuildCache(IReadOnlyCollection<ConfiguredProject> projects) {
            foreach (var project in projects) {
                if (project.IsDirty || project.BuildReason != null && (project.BuildReason.Flags.HasFlag(BuildReasonTypes.DependencyChanged) || project.BuildReason.Flags.HasFlag(BuildReasonTypes.InputsChanged))) {
                    if (project.DirectoryNode != null) {
                        if (project.BuildReason != null && project.BuildReason.Flags.HasFlag(BuildReasonTypes.ProjectOutputNotFound)) {
                            return;
                        }

                        project.DirectoryNode.RetrievePrebuilts = false;
                    }
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

            List<DirectoryNode> startNodes = new List<DirectoryNode>(DefaultDirectoryListCapacity);
            List<DirectoryNode> endNodes = new List<DirectoryNode>(DefaultDirectoryListCapacity);

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
                                }

                                if (contribution.DependencyManifest != null) {
                                    manifestMap[contribution.DependencyFile] = contribution.DependencyManifest;
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
                        try {
                            manifest = new DependencyManifest(string.Empty, XDocument.Parse(fileSystem.ReadAllText(manifestFilePath)));
                        } catch {
                            logger.Error("Failed to parse manifest: '{0}'.", manifestFilePath);
                            throw;
                        }
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

        private void AddDirectoryNodes(DependencyGraph graph, string nodeName, string nodeFullPath, out DirectoryNode startNode, out DirectoryNode endNode) {
            if (directoryNodeFactory == null) {
                directoryNodeFactory = new DirectoryNodeFactory(fileSystem);
                directoryNodeFactory.Initialize(graph);
            }

            var result = directoryNodeFactory.Create(graph, nodeName, nodeFullPath);
            startNode = result.Item1;
            endNode = result.Item2;
        }

        /// <summary>
        /// This is a reconciliation process. Applies the data from the state file to the current project.
        /// If the outputs look sane and safe then the project is removed from the build tree and the cached outputs are
        /// substituted in.
        /// </summary>
        internal BuildStateFile ApplyStateFile(BuildStateFile[] selectedStateFiles, string stateFileKey, ConfiguredProject project, bool skipNugetPackageHashCheck, ref bool hasLoggedUpToDate, BuildMetadata buildMetadata) {
            string solutionRoot = project.SolutionRoot;
            InputFilesDependencyAnalysisResult result = BeginTrackingInputFiles(selectedStateFiles, solutionRoot, skipNugetPackageHashCheck, buildMetadata);

            bool upToDate = result.IsUpToDate.GetValueOrDefault(true);
            bool hasTrackedFiles = result.TrackedFiles != null && result.TrackedFiles.Any();

            if (!upToDate && hasTrackedFiles) {
                MarkDirty(project, BuildReasonTypes.InputsChanged, string.Empty);
                return null;
            }

            if (upToDate && hasTrackedFiles && !hasLoggedUpToDate) {
                logger.Info("All tracked files are up to date: " + solutionRoot, null);
                hasLoggedUpToDate = true;
            }

            BuildStateFile stateFile = result.BuildStateFile;

            // See if we can skip this project because we can re-use the previous outputs
            if (stateFile != null) {
                if (this.PackageChecker == null) {
                    this.PackageChecker = new BuildCachePackageChecker(logger);
                }

                PackageChecker.Artifacts = null;

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
                    PackageChecker.Artifacts = artifacts;

                    foreach (var projectInTree in stateFile.Outputs) {
                        // Combine the original directory and the relative path to the project. This is done to get a unique path segment.
                        // This avoids false selection of projects where "A\B.proj" may exist across two directories.
                        string projectPath = Path.Combine(projectInTree.Value.Directory, projectInTree.Key);

                        if (projectFullPath.IndexOf(projectPath, StringComparison.OrdinalIgnoreCase) >= 0) {
                            // The selected build cache contained this project, next check the inputs/outputs

                            bool artifactContainsProject = PackageChecker.DoesArtifactContainProjectItem(project);
                            if (artifactContainsProject) {
                                return stateFile;
                            }

                            MarkDirty(project, BuildReasonTypes.OutputNotFoundInCachedBuild, (string) null);
                            return null;
                        }
                    }

                    // Output assembly is missing - check if this is allowed
                    //if (project.SkipCopyBuildProduct) {
                    //    return;
                    //}
                } else {
                    logger.Info($"No artifacts exist for '{stateFileKey}' or there are no project outputs.", null);
                }

                MarkDirty(project, BuildReasonTypes.ProjectOutputNotFound, (string) null);
                return null;
            }

            BuildReasonTypes type = BuildReasonTypes.CachedBuildNotFound;
            if (hasTrackedFiles) {
                // We have a balancing act here.
                // We want to build as few things as possible but also need to maintain
                // correctness.
                // If there is no state file present then we don't know if something is out of date
                // as we don't have the previous hashes to compare against.
                // If we have tracked inputs the we will also flag the item as 'InputsChanged'
                // This is because 'CachedBuildNotFound' is treated
                // specially as a performance optimization to prevent overbuilding the tree
                // and we want to disable this optimization here.
                type |= BuildReasonTypes.InputsChanged;
            }

            MarkDirty(project, type, (string) null);
            return null;
        }

        private TrackedMetadataFile AcquirePaketLockMetadata(string directory, string[] packageHashVersionExclusions) {
            if (!string.IsNullOrWhiteSpace(directory) && fileSystem != null) {
                // Get paket.lock if it exists.
                string paketLockFile = Path.Combine(directory, Constants.PaketLock);

                if (fileSystem.FileExists(paketLockFile)) {
                    PaketLockOperations paketLockOperations =
                        new PaketLockOperations(paketLockFile, fileSystem.ReadAllLines(paketLockFile));

                    string packageHash = PaketLockOperations.HashLockFile(paketLockOperations.LockFileContent, packageHashVersionExclusions);

                    return new TrackedMetadataFile(paketLockFile) {
                        PackageHash = packageHash,
                        PackageGroups = paketLockOperations.GetPackageInfo(),
                        Sha1 = packageHash,
                        TrackPackageHash = packageHashVersionExclusions != null && packageHashVersionExclusions.Length > 0
                    };
                }
            }

            return null;
        }

        internal BuildStateFile[] FilterStateFiles(BuildStateFile[] selectedStateFiles, string packageHash) {
            if (string.IsNullOrWhiteSpace(packageHash)) {
                return selectedStateFiles;
            }

            // Prefer artifacts where the package hash matches.
            var result = selectedStateFiles?.Where(x => string.Equals(x.PackageHash, packageHash)).ToArray();

            if (result != null && result.Any()) {
                logger.Info("Filtered out build state files which do not match package hash: '{0}'.", packageHash);

                return result;
            }

            return selectedStateFiles;
        }

        private InputFilesDependencyAnalysisResult BeginTrackingInputFiles(BuildStateFile[] selectedStateFiles, string solutionRoot, bool skipNugetPackageHashCheck, BuildMetadata buildMetadata) {
            if (!trackedInputs.ContainsKey(solutionRoot)) {
                trackedInputFilesCheck.SkipNuGetPackageHashCheck = skipNugetPackageHashCheck;

                List<TrackedMetadataFile> trackedMetadataFiles = new List<TrackedMetadataFile>();
                BuildStateFile[] buildStateFiles = selectedStateFiles;

                if (!skipNugetPackageHashCheck) {
                    TrackedMetadataFile paketLockMetadata = AcquirePaketLockMetadata(solutionRoot, buildMetadata?.PackageHashVersionExclusions);

                    if (paketLockMetadata != null) {
                        logger.Debug("Adding package hash metadata: '{0}' to tracked files.", paketLockMetadata.PackageHash);
                        trackedMetadataFiles.Add(paketLockMetadata);
                        buildStateFiles = FilterStateFiles(selectedStateFiles, paketLockMetadata.PackageHash);
                    }
                }

                logger.Info("Assessing state files for directory: '{0}'.", solutionRoot);

                List<TrackedInputFile> filesToTrack = trackedInputFilesCheck.GetFilesToTrack(solutionRoot);

                InputFilesDependencyAnalysisResult inputFilesAnalysisResult = trackedInputFilesCheck.PerformDependencyAnalysis(buildStateFiles, filesToTrack, trackedMetadataFiles);

                trackedInputs.Add(solutionRoot, inputFilesAnalysisResult);

                if (PipelineService != null) {
                    if (inputFilesAnalysisResult.TrackedFiles != null && inputFilesAnalysisResult.TrackedFiles.Any()) {
                        string solutionRootName = PathUtility.GetFileName(solutionRoot);
                        logger.Info($"Tracking {inputFilesAnalysisResult.TrackedFiles.Count} input files for {solutionRootName}", null);

                        if (inputFilesAnalysisResult.IsUpToDate == true && inputFilesAnalysisResult.BuildStateFile != null) {
                            BuildStateFile stateFile = inputFilesAnalysisResult.BuildStateFile;
                            logger.Info("State file {0} tracked files are up to date: {1}", stateFile.Id, inputFilesAnalysisResult.IsUpToDate);
                            logger.Info("Using state file: {0} -> {1} -> {2}:{3}", stateFile.Id, stateFile.BuildId, stateFile.Location, stateFile.BucketId.Tag);
                        }

                        PipelineService.TrackInputFileDependencies(solutionRootName, inputFilesAnalysisResult.TrackedFiles);
                    }

                    return inputFilesAnalysisResult;
                }
            }

            return trackedInputs[solutionRoot];
        }


        private BuildStateFile[] SelectStateFiles(string stateFileKey) {
            BuildStateFile[] selectedStateFiles = stateFiles.Where(x => string.Equals(x.BucketId.Tag, stateFileKey, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (selectedStateFiles.Length == 0) {
                logger.Info($"No state files available for {stateFileKey}. Build will not be able to use prebuilt objects.", null);
            }

            return selectedStateFiles;
        }

        private static void MarkDirty(ConfiguredProject project, BuildReasonTypes reasonTypes, string reasonDescription) {
            project.IsDirty = true;
            project.IncludeInBuild = true;
            project.MarkDirtyAndSetReason(reasonTypes, reasonDescription);
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
            logger.Info("ChangesToConsider:" + changesToConsider, null);
            logger.Info("DependencyRelationshipProcessing:" + dependencyProcessing, null);

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

            HashSet<string> h = new HashSet<string>(dirtyProjects, StringComparer.OrdinalIgnoreCase);

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
                filteredProjects = buildableProjects.Where(x => !(x is ConfiguredProject) || ((ConfiguredProject) x).IsDirty).ToList();
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
                        configuredProject.IncludeInBuild = false;
                        return false;
                    }
                }

                if (configuredProject.DirtyFiles?.Count > 0) {
                    return true;
                }
            }

            if (configuredProject != null) {
                var reason = configuredProject.BuildReason;

                return
                    configuredProject.IsDirty
                    && reason.Flags != BuildReasonTypes.CachedBuildNotFound && !reason.Flags.HasFlag(BuildReasonTypes.AlwaysBuild);
            }

            return false;
        }

        private void ApplyExtensibilityImposition(ExtensibilityImposition extensibilityImposition, List<ConfiguredProject> visualStudioProjects) {
            foreach (var file in extensibilityImposition.AlwaysBuildProjects) {
                var fileNameOfProject = Path.GetFileName(file);

                foreach (var project in visualStudioProjects) {
                    if (string.Equals(fileNameOfProject, Path.GetFileName(project.FullPath), StringComparison.OrdinalIgnoreCase)) {
                        project.IncludeInBuild = true;
                        project.MarkDirtyAndSetReason(BuildReasonTypes.AlwaysBuild);
                    }
                }
            }
        }

        private static List<TreePrinter.Node> DescribeChanges(IDependable dependable, bool showPath) {
            ConfiguredProject configuredProject = dependable as ConfiguredProject;
            var children = new List<TreePrinter.Node>();

            if (configuredProject != null) {
                if (configuredProject.RequiresBuilding()) {
                    if (showPath) {
                        children.Add(new TreePrinter.Node {
                            Name = "Path: " + configuredProject.FullPath,
                        });
                    }

                    if (configuredProject.DirtyFiles?.Count > 0) {
                        children.Add(
                            new TreePrinter.Node {
                                Name = "Dirty files",
                                Children = configuredProject.DirtyFiles.Select(s => new TreePrinter.Node {
                                    Name = s
                                }).ToList()
                            });
                    }

                    if (configuredProject.BuildReason != null) {
                        string reason = string.Empty;
                        if (!string.IsNullOrWhiteSpace(configuredProject.BuildReason.Description)) {
                            reason = "Reason: " + configuredProject.BuildReason.Description;
                        }

                        children.Add(new TreePrinter.Node {
                            Name = string.Format("Flags: {0}. {1}", configuredProject.BuildReason.Flags, reason)
                        });

                        if (configuredProject.BuildReason.ChangedDependentProjects != null)
                            children.Add(
                                new TreePrinter.Node {
                                    Name = "Changed dependencies",
                                    Children = configuredProject.BuildReason.ChangedDependentProjects.Select(s => new TreePrinter.Node {
                                        Name = s
                                    }).ToList()
                                });
                    }
                } else {
                    children.Add(
                        new TreePrinter.Node {
                            Name = "Building: false"
                        });
                }

                return children;
            }

            DirectoryNode node = dependable as DirectoryNode;
            if (node != null) {
                if (!node.IsPostTargets) {
                    children.Add(new TreePrinter.Node {
                        Name = "Is building any projects: " + node.IsBuildingAnyProjects
                    });
                }
            }

            return children;
        }

        /// <summary>
        /// Mark all projects in allProjects where the project depends on any one in projectsToFind.
        /// </summary>
        /// <param name="allProjects">The full projects list.</param>
        /// <param name="projectsToFind">The project name hashset to search for.</param>
        /// <returns>The list of projects that gets dirty because they depend on any project found in the search list.</returns>
        internal List<IDependable> MarkDirty(IReadOnlyCollection<IDependable> allProjects, HashSet<string> projectsToFind) {
            List<IDependable> affectedProjects = new List<IDependable>(DefaultDirectoryListCapacity);

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
                topLevelNode.Children = Describe(group, showPath).ToList();
                i++;

                topLevelNodes.Add(topLevelNode);
            }

            StringBuilder sb = new StringBuilder(4096);
            TreePrinter.Print(topLevelNodes, message => sb.Append(message));

            return sb.ToString();
        }

        private static IEnumerable<TreePrinter.Node> Describe(IReadOnlyList<IDependable> group, bool showPath) {
            foreach (IDependable dependable in group) {
                var changes = DescribeChanges(dependable, showPath);

                yield return new TreePrinter.Node {
                    Name = dependable.Id.Split(':').Last(),
                    Children = changes
                };
            }
        }
    }

    [Flags]
    internal enum BuildReasonTypes {
        None = 0,

        /// <summary>
        /// Indicates a file owned by this project has been modified
        /// </summary>
        ProjectItemChanged = 1,

        CachedBuildNotFound = 2,
        OutputNotFoundInCachedBuild = 4,
        ProjectOutputNotFound = 8,
        DependencyChanged = 16,
        AlwaysBuild = 32,
        Forced = 64,
        InputsChanged = 128,
        ProjectChanged = 256,
        RequiredByTextTemplate = 512,
    }
}
