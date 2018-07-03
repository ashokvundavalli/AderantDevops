using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Logging;
using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem.SolutionParser;
using Aderant.Build.VersionControl;
using Microsoft.Build.Construction;

namespace Aderant.Build.DependencyAnalyzer {
    internal class BuildSequencer {
        private readonly ILogger logger;
        private readonly Context context;
        private readonly ISolutionFileParser solutionParser;
        private readonly IFileSystem2 fileSystem;
        private readonly IVersionControlService versionControlService;

        public BuildSequencer(ILogger logger, Context context, ISolutionFileParser solutionParser, IFileSystem2 fileSystem, IVersionControlService versionControlService) {
            this.logger = logger;
            this.context = context;
            this.solutionParser = solutionParser;
            this.fileSystem = fileSystem;
            this.versionControlService = versionControlService;

            SolutionExtensionPatternsToIgnore = new List<string> {
                ".template.sln",
                ".custom.sln",
            };
        }

        /// <summary>
        /// Gets or sets the solution file names patterns to ignore.
        /// </summary>
        /// <value>The solution patterns to ignore.</value>
        public IEnumerable<string> SolutionExtensionPatternsToIgnore {
            get;
            set;
        }

        public Project CreateProject(string directory, BuildJobFiles instance, string buildFrom, ComboBuildType buildType, ProjectRelationshipProcessing dependencyProcessing, string buildConfiguration) {
            // This could also fail with a circular reference exception. If it does we cannot solve the problem.
            IEnumerable<IDependencyRef> filteredProjects;
            try {
                var analyzer = AnalyzeDependencies(directory, buildType, dependencyProcessing, buildConfiguration, out filteredProjects);

                // Determine the build groups to get maximum speed.
                List<List<IDependencyRef>> groups = null;//analyzer.GetBuildGroups(filteredProjects);

                // Create the dynamic build project file.
                BuildJob buildJob = new BuildJob(fileSystem);
                return buildJob.GenerateProject(groups, instance, buildFrom);
            } catch (CircularDependencyException ex) {
                logger.Error("Circular reference between projects: " + string.Join(", ", ex.Conflicts) + ". No solution is possible.");
                throw;
            }
        }

        private ProjectDependencyAnalyzer AnalyzeDependencies(string modulesDirectory, ComboBuildType buildType, ProjectRelationshipProcessing dependencyProcessing, string buildConfiguration, out IEnumerable<IDependencyRef> filteredProjects) {
            var analyzer = new ProjectDependencyAnalyzer(
                new CSharpProjectLoader(),
                new TextTemplateAnalyzer(fileSystem),
                fileSystem);

            analyzer.AddExclusionPattern("_BUILD_");
            analyzer.AddExclusionPattern("__");
            analyzer.AddExclusionPattern("Tests.Query");
            analyzer.AddExclusionPattern("Applications.DocuDraftAddIn");
            analyzer.AddExclusionPattern("UIAutomation");
            analyzer.AddExclusionPattern("UITest");
            analyzer.AddExclusionPattern("Applications.Marketing");
            analyzer.AddExclusionPattern("Workflow.Integration.Samples");
            analyzer.AddExclusionPattern("Aderant.Installation");
            analyzer.AddExclusionPattern("MomentumFileOpening");
            analyzer.AddSolutionFileNameFilter(@"Aderant.MatterCenterIntegration.Application\Package.sln");

            AnalyzerContext analyzerContext = new AnalyzerContext();
            analyzerContext.AddDirectory(modulesDirectory);

            // Get all changed files
            if (buildType == ComboBuildType.Changes) {
                logger.Debug("Querying version control provider for pending changes...");

                IEnumerable<IPendingChange> pendingChanges = versionControlService.GetPendingChanges(modulesDirectory);
                analyzerContext.PendingChanges = pendingChanges.ToList();
            }

            analyzerContext.ModulesDirectory = modulesDirectory;
            List<IDependencyRef> visualStudioProjects = null; //analyzer.GetDependencyGraph(analyzerContext);

            IEnumerable<VisualStudioProject> studioProjects = visualStudioProjects.OfType<VisualStudioProject>();

            AssignProjectConfiguration(buildConfiguration, studioProjects);

            // According to options, find out which projects are selected to build.
            filteredProjects = GetProjectsBuildList(visualStudioProjects, buildType, dependencyProcessing);

            Validate(filteredProjects);
            return analyzer;
        }

        /// <summary>
        /// Sets the <see cref="VisualStudioProject.IncludeInBuild"/> state
        /// </summary>
        private void AssignProjectConfiguration(string buildConfiguration, IEnumerable<VisualStudioProject> studioProjects) {
            var projects = new List<VisualStudioProject>(studioProjects);

            foreach (IGrouping<string, VisualStudioProject> grouping in studioProjects.GroupBy(g => g.SolutionRoot)) {
                string solutionRoot = grouping.Key;

                foreach (string file in fileSystem.GetFiles(solutionRoot, "*.sln", false)) {
                    bool readSolution = SolutionExtensionPatternsToIgnore.All(pattern => !file.EndsWith(pattern, StringComparison.OrdinalIgnoreCase));

                    if (readSolution) {
                        ParseResult result = solutionParser.Parse(file);

                        foreach (ProjectInSolution projectInSolution in result.ProjectsInOrder) {
                            if (projectInSolution.ProjectType == SolutionProjectType.SolutionFolder) {
                                continue;
                            }

                            ProjectConfigurationInSolution config;

                            if (projectInSolution.ProjectConfigurations.TryGetValue(buildConfiguration, out config)) {
                                if (config.IncludeInBuild) {
                                    var foundProject = IncludeInBuild(result.SolutionFile, config, projectInSolution.AbsolutePath, projects);

                                    if (foundProject != null) {
                                        projects.Remove(foundProject);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // If we have any projects left, then they are orphaned as they belong to no solution
            if (projects.Any()) {
                logger.Warning("Orphaned projects: ");
                foreach (var project in projects) {
                    logger.Warning(project.Path);
                }
            }
        }

        /// <summary>
        /// According to options, find out which projects are selected to build.
        /// </summary>
        /// <param name="visualStudioProjects">All the projects list.</param>
        /// <param name="comboBuildType">Build the current branch, the changed files since forking from master, or all?</param>
        /// <param name="dependencyProcessing">Build the directly affected downstream projects, or recursively search for all downstream projects, or none?</param>
        /// <returns></returns>
        private IEnumerable<IDependencyRef> GetProjectsBuildList(List<IDependencyRef> visualStudioProjects, ComboBuildType comboBuildType, ProjectRelationshipProcessing dependencyProcessing) {
            // Get all the dirty projects due to user's modification.
            var dirtyProjects = visualStudioProjects.Where(x => (x as VisualStudioProject)?.IsDirty == true).Select(x => x.Name).ToList();
            HashSet<string> h = new HashSet<string>();
            h.UnionWith(dirtyProjects);
            // According to DownStream option, either mark the direct affected or all the recursively affected downstream projects as dirty.
            switch (dependencyProcessing) {
                case ProjectRelationshipProcessing.Direct:
                    MarkDirty(visualStudioProjects, h);
                    break;
                case ProjectRelationshipProcessing.Transitive:
                    MarkDirtyAll(visualStudioProjects, h);
                    break;
            }

            // Get all projects that are either visualStudio projects and dirty, or not visualStudio projects. Or say, skipped the unchanged csproj projects.
            IEnumerable<IDependencyRef> filteredProjects;
            if (comboBuildType == ComboBuildType.All) {
                filteredProjects = visualStudioProjects;
            } else {
                filteredProjects = visualStudioProjects.Where(x => (x as VisualStudioProject)?.IsDirty != false).ToList();

                logger.Info("Changed projects:");
                foreach (var pp in filteredProjects) {
                    logger.Info("* " + pp.Name);
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
        internal List<IDependencyRef> MarkDirty(List<IDependencyRef> allProjects, HashSet<string> projectsToFind) {

            var p = allProjects.Where(x => (x as VisualStudioProject)?.DependsOn?.Select(y => y.Name).Intersect(projectsToFind).Any() == true).ToList();
            p.ForEach(x => {
                if (x is VisualStudioProject) {
                    ((VisualStudioProject)x).IsDirty = true;
                }
            });
            return p;
        }

        /// <summary>
        /// Recursively mark all projects in allProjects where the project depends on any one in projectsToFind until no more parent project is found.
        /// </summary>
        internal void MarkDirtyAll(List<IDependencyRef> allProjects, HashSet<string> projectsToFind) {

            int newCount=-1;

            List<IDependencyRef> p;
            while(newCount!=0) {
                p = MarkDirty(allProjects, projectsToFind).ToList();
                newCount = p.Count;
                var newSearchList = new HashSet<string>();
                newSearchList.UnionWith(p.Select(x => x.Name));
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

        private VisualStudioProject IncludeInBuild(string solutionFile, ProjectConfigurationInSolution configuration, string absolutePath, List<VisualStudioProject> visualStudioProjects) {
            foreach (VisualStudioProject project in visualStudioProjects) {
                if (string.Equals(project.Path, absolutePath, StringComparison.OrdinalIgnoreCase)) {
                    project.IncludeInBuild = true;
                    project.SolutionFile = solutionFile;
                    project.ProjectBuildConfiguration = new ProjectBuildConfiguration(/*Debug or Release*/configuration.ConfigurationName, /*x86 etc*/configuration.PlatformName );

                    return project;
                }
            }

            return null;
        }

        public XElement CreateProjectDocument(Project project) {
            BuildElementVisitor visitor = new ParallelBuildVisitor();
            project.Accept(visitor);

            return visitor.GetDocument();
        }

        internal static void SaveBuildProject(string file, XElement projectDocument) {
            var settings = new XmlWriterSettings {
                NewLineOnAttributes = true,
                Indent = true,
                IndentChars = "  ",
                Encoding = Encoding.UTF8,
                CloseOutput = true, 
            };

            using (var writer = XmlWriter.Create(file, settings)) {
                projectDocument.WriteTo(writer);
            }
        }
    }

}
