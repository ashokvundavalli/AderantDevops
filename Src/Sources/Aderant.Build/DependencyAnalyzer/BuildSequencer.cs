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

namespace Aderant.Build.DependencyAnalyzer {

    [Export(typeof(ISequencer))]
    internal class BuildSequencer : ISequencer {
        private readonly IFileSystem2 fileSystem;
        private readonly ILogger logger;

        [ImportingConstructor]
        public BuildSequencer(ILogger logger, IFileSystem2 fileSystem) {
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        public Project CreateProject(Context context, OrchestrationFiles files, DependencyGraph graph) {
            AddInitializeAndCompletionNodes(graph);

            List<IDependable> projectsInDependencyOrder = graph.GetDependencyOrder();

            // According to options, find out which projects are selected to build.
            var filteredProjects = GetProjectsBuildList(
                projectsInDependencyOrder,
                context.GetChangeConsiderationMode(),
                context.GetRelationshipProcessingMode());

            List<List<IDependable>> groups = graph.GetBuildGroups(filteredProjects);

            var job = new BuildPipeline(fileSystem);
            return job.GenerateProject(groups, files, null);
        }

        private void AddInitializeAndCompletionNodes(DependencyGraph graph) {
            var projects = graph.Nodes
                .OfType<ConfiguredProject>()
                .ToList();

            var grouping = projects.GroupBy(g => Path.GetDirectoryName(g.SolutionFile), StringComparer.OrdinalIgnoreCase);

            foreach (var level in grouping) {
                if (level.Any(g => g.IsDirty)) {
                    IArtifact initializeNode;
                    string solutionDirectoryName = Path.GetFileName(level.Key);

                    // Create a new node that represents the start of a directory
                    graph.Add(initializeNode = new DirectoryNode(solutionDirectoryName, level.Key, false));

                    // Create a new node that represents the completion of a directory
                    DirectoryNode completionNode = new DirectoryNode(solutionDirectoryName, level.Key, true);

                    graph.Add(completionNode);
                    completionNode.AddResolvedDependency(null, initializeNode);

                    foreach (var project in projects
                        .Where(p => string.Equals(Path.GetDirectoryName(p.SolutionFile), level.Key, StringComparison.OrdinalIgnoreCase))) {

                        TriggerP2PBuildShim(project);

                        project.AddResolvedDependency(null, initializeNode);
                        completionNode.AddResolvedDependency(null, project);
                    }
                }
            }
        }

        private static void TriggerP2PBuildShim(ConfiguredProject project) {
            // TODO: Because we can't build *just* the projects that have changed, mark anything in this container as dirty to trigger a build for it
            project.IsDirty = true;
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
        /// <returns></returns>
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

                foreach (var pp in filteredProjects) {
                    ConfiguredProject configuredProject = pp as ConfiguredProject;

                    if (configuredProject != null) {
                        if (sb == null) {
                            sb = new StringBuilder();
                            sb.AppendLine("Changed projects: ");
                        }

                        sb.AppendLine("* " + configuredProject.Id);

                        if (configuredProject.DirtyFiles != null) {
                            foreach (string dirtyFile in configuredProject.DirtyFiles) {
                                sb.AppendLine("    " + dirtyFile);
                            }
                        }
                    }
                }

                if (sb != null) {
                    logger.Info(sb.ToString());
                }
            }

            return filteredProjects;
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
}
