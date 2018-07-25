﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Model;
using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.DependencyAnalyzer {

    [Export(typeof(ISequencer))]
    internal class BuildSequencer : ISequencer {
        private readonly IFileSystem2 fileSystem;

        [ImportingConstructor]
        public BuildSequencer(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        public Project CreateProject(Context context, BuildJobFiles files, DependencyGraph graph) {
            List<IDependable> projectsInDependencyOrder = graph.GetDependencyOrder();
            List<List<IDependable>> groups = graph.GetBuildGroups(projectsInDependencyOrder);

            var job = new BuildJob(fileSystem);
            return job.GenerateProject(groups, files, null);
        }

        /// <summary>
        /// According to options, find out which projects are selected to build.
        /// </summary>
        /// <param name="visualStudioProjects">All the projects list.</param>
        /// <param name="comboBuildType">Build the current branch, the changed files since forking from master, or all?</param>
        /// <param name="dependencyProcessing">
        /// Build the directly affected downstream projects, or recursively search for all
        /// downstream projects, or none?
        /// </param>
        /// <returns></returns>
        private IEnumerable<IDependencyRef> GetProjectsBuildList(List<IDependencyRef> visualStudioProjects, ComboBuildType comboBuildType, ProjectRelationshipProcessing dependencyProcessing) {
            //// Get all the dirty projects due to user's modification.
            //var dirtyProjects = visualStudioProjects.Where(x => (x as VisualStudioProject)?.IsDirty == true).Select(x => x.Name).ToList();
            //HashSet<string> h = new HashSet<string>();
            //h.UnionWith(dirtyProjects);
            //// According to DownStream option, either mark the direct affected or all the recursively affected downstream projects as dirty.
            //switch (dependencyProcessing) {
            //    case ProjectRelationshipProcessing.Direct:
            //        MarkDirty(visualStudioProjects, h);
            //        break;
            //    case ProjectRelationshipProcessing.Transitive:
            //        MarkDirtyAll(visualStudioProjects, h);
            //        break;
            //}

            //// Get all projects that are either visualStudio projects and dirty, or not visualStudio projects. Or say, skipped the unchanged csproj projects.
            //IEnumerable<IDependencyRef> filteredProjects;
            //if (comboBuildType == ComboBuildType.All) {
            //    filteredProjects = visualStudioProjects;
            //} else {
            //    filteredProjects = visualStudioProjects.Where(x => (x as VisualStudioProject)?.IsDirty != false).ToList();

            //    logger.Info("Changed projects:");
            //    foreach (var pp in filteredProjects) {
            //        logger.Info("* " + pp.Name);
            //    }
            //}

            //return filteredProjects;
            return null;
        }

        /// <summary>
        /// Mark all projects in allProjects where the project depends on any one in projectsToFind.
        /// </summary>
        /// <param name="allProjects">The full projects list.</param>
        /// <param name="projectsToFind">The project name hashset to search for.</param>
        /// <returns>The list of projects that gets dirty because they depend on any project found in the search list.</returns>
        internal List<IDependable> MarkDirty(List<IDependable> allProjects, HashSet<string> projectsToFind) {
            var p = allProjects.Where(x => (x as ConfiguredProject)?.GetDependencies().Select(y => y.Id).Intersect(projectsToFind).Any() == true).ToList();
            p.ForEach(
                x => {
                    if (x is ConfiguredProject) {
                        ((ConfiguredProject)x).IsDirty = true;
                    }
                });
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

        private void Validate(IEnumerable<IDependencyRef> visualStudioProjects) {
            //var validator = new ProjectGuidValidator();

            //var query = visualStudioProjects.GroupBy(x => x.ProjectGuid)
            //    .Where(g => g.Count() > 1)
            //    .Select(y => new { Element = y.Key, Counter = y.Count() })
            //    .ToList();

            //if (query.Any()) {
            //    throw new Exception("There are projects with duplicate project GUIDs: " + string.Join(", ", query.Select(s => s.Element)));
            //}
        }
    }
}
