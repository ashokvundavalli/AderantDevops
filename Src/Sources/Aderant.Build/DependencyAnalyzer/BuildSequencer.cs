using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl.Model;

namespace Aderant.Build.DependencyAnalyzer {

    [Export(typeof(ISequencer))]
    internal class BuildSequencer : ISequencer {
        private readonly IFileSystem2 fileSystem;
        private readonly ILogger logger;
        private List<BuildStateFile> stateFiles;

        [ImportingConstructor]
        public BuildSequencer(ILogger logger, IFileSystem2 fileSystem) {
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        public Project CreateProject(BuildOperationContext context, OrchestrationFiles files, DependencyGraph graph) {
            bool isPullRequest = context.BuildMetadata.IsPullRequest;
            bool isDesktopBuild = context.IsDesktopBuild;

            if (context.StateFiles == null) {
                FindStateFiles(context);

                if (stateFiles != null) {
                    EvictDeletedProjects(context);
                }
            }

            AddInitializeAndCompletionNodes(isPullRequest, isDesktopBuild, graph);

            List<IDependable> projectsInDependencyOrder = graph.GetDependencyOrder();

            // According to options, find out which projects are selected to build.
            var filteredProjects = GetProjectsBuildList(
                projectsInDependencyOrder,
                context.GetChangeConsiderationMode(),
                context.GetRelationshipProcessingMode());

            List<List<IDependable>> groups = graph.GetBuildGroups(filteredProjects);

            StringBuilder sb = null;

            foreach (var group in groups) {
                foreach (var item in group) {
                    sb = DescribeChanges(item, sb);
                }
            }

            if (sb != null) {
                logger.Info(sb.ToString());
            }

            var pipeline = new BuildPipeline(fileSystem);
            return pipeline.GenerateProject(groups, files, null);
        }

        private void EvictDeletedProjects(BuildOperationContext context) {
            // here we evict deleted projects from the previous builds metadata
            // This is so we do not consider the outputs of this project in the artifact restore phase
            if (context.SourceTreeMetadata.Changes != null) {
                IEnumerable<SourceChange> deletes = context.SourceTreeMetadata.Changes.Where(c => c.Status == FileStatus.Deleted);
                foreach (var delete in deletes) {

                    foreach (var file in stateFiles) {
                        if (delete.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) {
                            if (file.Outputs.ContainsKey(delete.Path)) {
                                file.Outputs.Remove(delete.Path);
                            }
                        }
                    }
                }
            }

        }

        private void FindStateFiles(BuildOperationContext context) {
            var files = GetBuildStateFile(context);
            if (files != null) {
                this.stateFiles = context.StateFiles = files;

                foreach (var file in stateFiles) {
                    logger.Info($"Selected state file: {file.Id}:{file.DropLocation}");
                }
            }
        }

        private static List<BuildStateFile> GetBuildStateFile(BuildOperationContext context) {
            List<BuildStateFile> stateFiles = new List<BuildStateFile>();

            // Here we select an appropriate tree to reuse
            // TODO: This needs way more validation
            var buildStateMetadata = context.BuildStateMetadata;

            if (buildStateMetadata != null && context.SourceTreeMetadata != null)
                foreach (var bucketId in context.SourceTreeMetadata.GetBuckets()) {
                    BuildStateFile stateFile = buildStateMetadata.BuildStateFiles.FirstOrDefault(s => string.Equals(s.BucketId.Id, bucketId.Id));

                    if (stateFile != null) {
                        stateFiles.Add(stateFile);
                    }
                }

            return stateFiles;
        }

        private void AddInitializeAndCompletionNodes(bool isPullRequest, bool isDesktopBuild, DependencyGraph graph) {
            var projects = graph.Nodes
                .OfType<ConfiguredProject>()
                .ToList();

            var grouping = projects.GroupBy(g => Path.GetDirectoryName(g.SolutionFile), StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouping) {
                List<ConfiguredProject> dirtyProjects = group.Where(g => g.IsDirty).ToList();

                if (AddNodes(dirtyProjects, isPullRequest, isDesktopBuild)) {
                    string tag = string.Join(", ", dirtyProjects.Select(s => s.Id));

                    IArtifact initializeNode;
                    string solutionDirectoryName = Path.GetFileName(group.Key);

                    // Create a new node that represents the start of a directory
                    graph.Add(initializeNode = new DirectoryNode(solutionDirectoryName, group.Key, false));

                    // Create a new node that represents the completion of a directory
                    DirectoryNode completionNode = new DirectoryNode(solutionDirectoryName, group.Key, true);

                    graph.Add(completionNode);
                    completionNode.AddResolvedDependency(null, initializeNode);

                    var stateFile = SelectStateFile(solutionDirectoryName);

                    foreach (var project in projects
                        .Where(p => string.Equals(Path.GetDirectoryName(p.SolutionFile), group.Key, StringComparison.OrdinalIgnoreCase))) {

                        ApplyStateFile(stateFile, solutionDirectoryName, tag, project);

                        project.AddResolvedDependency(null, initializeNode);
                        completionNode.AddResolvedDependency(null, project);
                    }
                }
            }
        }

        private bool AddNodes(List<ConfiguredProject> dirtyProjects, bool isPullRequest, bool isDesktopBuild) {
            // TODO: Not sure why we need this function?
            if (dirtyProjects.Any()) {
                return true;
            }

            if (!isPullRequest && !isDesktopBuild) {
                return true;
            }

            if (isDesktopBuild) {
                return true;
            }

            return true;
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

                if (stateFile.Outputs != null && artifactsExist)
                    foreach (var projectInTree in stateFile.Outputs) {
                        // The selected build cache contained this project, next check the inputs/outputs
                        if (projectFullPath.IndexOf(projectInTree.Key, StringComparison.OrdinalIgnoreCase) >= 0) {

                            bool artifactContainsProject = DoesArtifactContainProjectItem(project, artifacts);
                            if (artifactContainsProject) {
                                return;
                            }

                            MarkDirty(tag, project, InclusionReason.ArtifactsNotFound);
                            return;
                        }
                    }

                MarkDirty(tag, project, InclusionReason.ProjectOutputNotFound);
                return;
            }

            MarkDirty(tag, project, InclusionReason.BuildTreeNotFound | InclusionReason.None);
        }

        private bool DoesArtifactContainProjectItem(ConfiguredProject project, ICollection<ArtifactManifest> artifacts) {
            var outputFile = project.GetOutputAssemblyWithExtension();

            List<ArtifactManifest> misses = null;

            foreach (ArtifactManifest s in artifacts) {
                foreach (ArtifactItem file in s.Files) {
                    if (string.Equals(file.File, outputFile, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }

                if (misses == null) {
                    misses = new List<ArtifactManifest>();
                }

                misses.Add(s);
            }

            if (misses != null && misses.Count > 0) {
                var error = string.Join("|", misses.Select(s => string.Format("{0} ({1})", s.Id, s.InstanceId)));
                logger.Info($"Looked for {outputFile} but it was not found in packages: [{error}]");
            }

            return false;
        }

        private BuildStateFile SelectStateFile(string stateFileKey) {
            foreach (var file in stateFiles) {
                if (string.Equals(file.BucketId.Tag, stateFileKey, StringComparison.OrdinalIgnoreCase)) {
                    // Reject files that provide no value
                    if (file.Outputs == null || file.Outputs.Count == 0) {
                        continue;
                    }

                    if (file.Artifacts == null || file.Artifacts.Count == 0) {
                        continue;
                    }

                    return file;
                }
            }

            return null;
        }

        private static void MarkDirty(string tag, ConfiguredProject project, InclusionReason reason) {
            project.IsDirty = true;

            if (project.InclusionDescriptor == null) {
                project.InclusionDescriptor = new InclusionDescriptor();
            }

            project.InclusionDescriptor.Tag = tag;
            project.InclusionDescriptor.Reason |= reason;
        }

        /// <summary>
        /// According to options, find out which projects are selected to build.
        /// </summary>
        /// <param name="visualStudioProjects">All the projects list.</param>
        /// <param name="changesToConsider">Build the current branch, the changed files since forking from master, or all?</param>
        /// <param name="dependencyProcessing">
        /// Build the directly affected downstream projects, or recursively search for all
        /// downstream projects, or none?
        /// </param>
        private List<IDependable> GetProjectsBuildList(List<IDependable> visualStudioProjects, ChangesToConsider changesToConsider, DependencyRelationshipProcessing dependencyProcessing) {
            // Get all the dirty projects due to user's modification.
            var dirtyProjects = visualStudioProjects.Where(x => (x as ConfiguredProject)?.IsDirty == true).Select(x => x.Id).ToList();
            HashSet<string> h = new HashSet<string>();
            h.UnionWith(dirtyProjects);

            // According to DownStream option, either mark the direct affected or all the recursively affected downstream projects as dirty.
            switch (dependencyProcessing) {
                case DependencyRelationshipProcessing.Direct:
                    MarkDirty(visualStudioProjects, h);
                    break;
                case DependencyRelationshipProcessing.Transitive:
                    MarkDirtyAll(visualStudioProjects, h);
                    break;
            }

            // Get all projects that are either visualStudio projects and dirty, or not visualStudio projects. Or say, skipped the unchanged csproj projects.
            List<IDependable> filteredProjects;
            if (changesToConsider == ChangesToConsider.None) {
                filteredProjects = visualStudioProjects;
            } else {
                filteredProjects = visualStudioProjects.Where(x => (x as ConfiguredProject)?.IsDirty != false).ToList();
            }

            return filteredProjects;
        }

        private static StringBuilder DescribeChanges(IDependable pp, StringBuilder sb) {
            ConfiguredProject configuredProject = pp as ConfiguredProject;

            if (configuredProject != null) {
                if (sb == null) {
                    sb = new StringBuilder();
                    sb.AppendLine("Changed projects: ");
                }

                sb.AppendLine("* " + configuredProject.FullPath);

                if (configuredProject.InclusionDescriptor != null) {
                    sb.AppendLine("    " + "Reason: " + configuredProject.InclusionDescriptor.Reason);
                }

                if (configuredProject.DirtyFiles != null) {
                    foreach (string dirtyFile in configuredProject.DirtyFiles) {
                        sb.AppendLine("    " + dirtyFile);
                    }
                }
            }

            return sb;
        }

        /// <summary>
        /// Mark all projects in allProjects where the project depends on any one in projectsToFind.
        /// </summary>
        /// <param name="allProjects">The full projects list.</param>
        /// <param name="projectsToFind">The project name hashset to search for.</param>
        /// <returns>The list of projects that gets dirty because they depend on any project found in the search list.</returns>
        internal List<IDependable> MarkDirty(List<IDependable> allProjects, HashSet<string> projectsToFind) {
            var p = allProjects
                .Where(
                    x => (x as ConfiguredProject)?.GetDependencies()
                         .Select(y => y.Id)
                         .Intersect(projectsToFind).Any() == true).ToList();

            foreach (var x in p) {
                ConfiguredProject project = x as ConfiguredProject;
                if (project != null) {
                    project.IsDirty = true;
                    if (project.InclusionDescriptor == null) {
                        project.InclusionDescriptor = new InclusionDescriptor();
                    }

                    project.InclusionDescriptor.Reason |= InclusionReason.DependencyChanged;
                }
            }

            return p;
        }

        /// <summary>
        /// Recursively mark all projects in allProjects where the project depends on any one in projectsToFind until no more
        /// parent project is found.
        /// </summary>
        internal void MarkDirtyAll(List<IDependable> allProjects, HashSet<string> projectsToFind) {
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
    }

    [Flags]
    internal enum InclusionReason {
        None = 1,
        ProjectFileChanged = 2,
        BuildTreeNotFound = 4,
        ArtifactsNotFound = 8,
        ProjectOutputNotFound = 16,
        DependencyChanged = 32,
    }
}
