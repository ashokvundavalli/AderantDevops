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
using Aderant.Build.Tasks;
using Aderant.Build.VersionControl;

namespace Aderant.Build.DependencyAnalyzer {

    [Export(typeof(ISequencer))]
    internal class BuildSequencer : ISequencer {
        private readonly IFileSystem2 fileSystem;
        private readonly ILogger logger;
        private BuildStateFile stateFile;

        [ImportingConstructor]
        public BuildSequencer(ILogger logger, IFileSystem2 fileSystem) {
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        public Project CreateProject(BuildOperationContext context, OrchestrationFiles files, DependencyGraph graph) {
            bool isPullRequest = context.BuildMetadata.IsPullRequest;
            bool isDesktopBuild = context.IsDesktopBuild;

            if (context.StateFile == null) {
                FindStateFile(context);

                if (stateFile != null) {
                    EvictDeletedProjects(context);
                }
            }

            AddInitializeAndCompletionNodes(context.StateFile, isPullRequest, isDesktopBuild, graph);

            List<IDependable> projectsInDependencyOrder = graph.GetDependencyOrder();

            // According to options, find out which projects are selected to build.
            var filteredProjects = GetProjectsBuildList(
                projectsInDependencyOrder,
                context.GetChangeConsiderationMode(),
                context.GetRelationshipProcessingMode());

            List<List<IDependable>> groups = graph.GetBuildGroups(filteredProjects);

            var pipeline = new BuildPipeline(fileSystem);
            return pipeline.GenerateProject(groups, files, null);
        }

        private void EvictDeletedProjects(BuildOperationContext context) {
            // here we evict deleted projects from the previous builds metadata
            // This is so we do not consider the outputs of this project in the artifact restore phase
            IEnumerable<SourceChange> deletes = context.SourceTreeMetadata.Changes.Where(c => c.Status == FileStatus.Deleted);
            foreach (var delete in deletes) {
                if (delete.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) {
                    if (stateFile.Outputs.ContainsKey(delete.Path)) {
                        stateFile.Outputs.Remove(delete.Path);
                    }
                }
            }
        }

        private void FindStateFile(BuildOperationContext context) {
            BuildStateFile file = GetBuildStateFile(context);
            if (file != null) {
                this.stateFile = context.StateFile = file;
            }
        }

        private static BuildStateFile GetBuildStateFile(BuildOperationContext context) {
            // Here we select an appropriate tree to reuse
            // TODO: This needs way more validation
            var buildStateMetadata = context.BuildStateMetadata;

            if (buildStateMetadata != null && context.SourceTreeMetadata != null)
                foreach (var bucketId in context.SourceTreeMetadata.BucketIds) {
                    BuildStateFile stateFile = buildStateMetadata.BuildStateFiles.FirstOrDefault(s => string.Equals(s.TreeSha, bucketId.Id));

                    if (stateFile != null) {
                        return stateFile;
                    }
                }

            return null;
        }

        private void AddInitializeAndCompletionNodes(BuildStateFile stateFile, bool isPullRequest, bool isDesktopBuild, DependencyGraph graph) {
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

                    foreach (var project in projects
                        .Where(p => string.Equals(Path.GetDirectoryName(p.SolutionFile), group.Key, StringComparison.OrdinalIgnoreCase))) {

                        TryReuseExistingBuild(solutionDirectoryName, tag, project);

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

        private void TryReuseExistingBuild(string artifactPublisher, string tag, ConfiguredProject project) {
            if (String.Equals(project.GetOutputAssemblyWithExtension(), "UnitTest.Deployment.Client.dll", StringComparison.OrdinalIgnoreCase)) {
                //System.Diagnostics.Debugger.Launch();
            }

            // See if we can skip this project because we can re-use the previous outputs
            if (stateFile != null) {
                string projectFullPath = project.FullPath;

                bool artifactsExist = false;

                ICollection<ArtifactManifest> artifacts;
                if (stateFile.Artifacts.TryGetValue(artifactPublisher, out artifacts)) {
                    if (artifacts != null) {
                        artifactsExist = true;
                    }
                }

                foreach (var projectInTree in stateFile.Outputs) {
                    // The selected build cache contained this project, next check the inputs/outputs
                    if (projectFullPath.IndexOf(projectInTree.Key, StringComparison.OrdinalIgnoreCase) >= 0) {
                        if (artifactsExist) {
                            bool artifactContainsProject = artifacts.SelectMany(s => s.Files).Any(f => string.Equals(f.File, project.GetOutputAssemblyWithExtension(), StringComparison.OrdinalIgnoreCase));
                            if (artifactContainsProject) {
                                return;
                            }
                        }
                        MarkDirty(tag, project, InclusionReason.ArtifactsNotFound);
                        return;
                    }
                }

                MarkDirty(tag, project, InclusionReason.ProjectNotFound);
                return;
            }

            MarkDirty(tag, project, InclusionReason.BuildTreeNotFound | InclusionReason.ChangedFileDependency);
        }

        private static void MarkDirty(string tag, ConfiguredProject project, InclusionReason reason) {
            // TODO: Because we can't build *just* the projects that have changed, mark anything in this container as dirty to trigger a build for it
            project.IsDirty = true;
            project.InclusionDescriptor = new InclusionDescriptor {
                Tag = tag,
                Reason = reason
            };
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

                StringBuilder sb = null;
                foreach (var dependable in filteredProjects) {
                    sb = DescribeChanges(dependable, sb);
                }

                if (sb != null) {
                    logger.Info(sb.ToString());
                }
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
        ChangedFileDependency = 2,
        BuildTreeNotFound = 4,
        ArtifactsNotFound = 8,
        ProjectNotFound = 16
    }
}
