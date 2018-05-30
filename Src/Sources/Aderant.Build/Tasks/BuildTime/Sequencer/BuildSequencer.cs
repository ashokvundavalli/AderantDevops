using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.MSBuild;
using Aderant.Build.Providers;
using Aderant.Build.Tasks.BuildTime.ProjectDependencyAnalyzer;
using Microsoft.Build.Construction;

namespace Aderant.Build.Tasks.BuildTime.Sequencer {
    internal class BuildSequencer {
        private readonly ILogger logger;
        private readonly Context context;
        private readonly ISolutionFileParser solutionParser;
        private readonly RepositoryInfoProvider repositoryInfoProvider;
        private readonly IFileSystem2 fileSystem;

        public BuildSequencer(ILogger logger, Context context, ISolutionFileParser solutionParser, RepositoryInfoProvider repositoryInfoProvider, IFileSystem2 fileSystem) {
            this.logger = logger;
            this.context = context;
            this.solutionParser = solutionParser;
            this.repositoryInfoProvider = repositoryInfoProvider;
            this.fileSystem = fileSystem;
        }

        public Project CreateProject(string modulesDirectory, IModuleProvider moduleProvider, IEnumerable<string> modulesInBuild, string comboBuildProjectFile) {
            // This could also fail with a circular reference exception. If it does we cannot solve the problem.
            try {
                var analyzer = new ProjectDependencyAnalyzer.ProjectDependencyAnalyzer(new CSharpProjectLoader(), new TextTemplateAnalyzer(fileSystem), fileSystem);

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
                
                AnalyzerContext analyzerContext = new AnalyzerContext(modulesDirectory);

                // Get all changed files list
                if (context.ComboBuildType != ComboBuildType.All) {
                    List<string> files = new ChangesetResolver(this.context, modulesDirectory).ChangedFiles;
                    analyzerContext.SetFilesList(files);
                }

                List<IDependencyRef> visualStudioProjects = analyzer.GetDependencyOrder(analyzerContext);

                IEnumerable<VisualStudioProject> studioProjects = visualStudioProjects.OfType<VisualStudioProject>();

                foreach (IGrouping<string, VisualStudioProject> projects in studioProjects.GroupBy(g => g.SolutionRoot)) {
                    string solutionRoot = projects.Key;

                    foreach (string file in fileSystem.GetFiles(solutionRoot, Path.GetFileName(projects.Key) + ".sln", false, true)) {
                        ParseResult result = solutionParser.Parse(file);

                        foreach (ProjectInSolution projectInSolution in result.ProjectsInOrder) {
                            if (projectInSolution.ProjectType == SolutionProjectType.SolutionFolder) {
                                continue;
                            }

                            if (projectInSolution.ProjectConfigurations.TryGetValue("Debug|Any CPU", out ProjectConfigurationInSolution config)) {
                                if (config.IncludeInBuild) {
                                    IncludeInBuild(result, config, projectInSolution.AbsolutePath, studioProjects);
                                }
                            }
                        }
                    }
                }

                // According to options, find out which projects are selected to build.
                IEnumerable<IDependencyRef> filteredProjects = GetProjectsBuildList(visualStudioProjects);

                Validate(filteredProjects.Cast<VisualStudioProject>().ToList());

                // Determine the build groups to get maximum speed.
                List<List<IDependencyRef>> groups = analyzer.GetBuildGroups(filteredProjects);

                // Create the dynamic build project file.
                DynamicProject dynamicProject = new DynamicProject(context, new PhysicalFileSystem(modulesDirectory));
                return dynamicProject.GenerateProject(modulesDirectory, groups, comboBuildProjectFile);
            } catch (CircularDependencyException ex) {
                logger.Error("Circular reference between projects: " + string.Join(", ", ex.Conflicts) + ". No solution is possible.");
                throw;
            }
        }

        /// <summary>
        /// According to options, find out which projects are selected to build.
        /// </summary>
        /// <param name="visualStudioProjects">All the projects list.</param>
        /// <returns></returns>
        private IEnumerable<IDependencyRef> GetProjectsBuildList(List<IDependencyRef> visualStudioProjects) {
            // If we're building everything we don't need to report any projects to Roper.
            if (context.ComboBuildType == ComboBuildType.All) {
                return visualStudioProjects;
            }

            // Get all the dirty projects due to user's modification.
            HashSet<string> dirtyProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            dirtyProjects.UnionWith(visualStudioProjects.Where(x => (x as VisualStudioProject)?.IsDirty == true).Select(x => x.Name).ToList());
            // According to DownStream option, either mark the direct affected or all the recursively affected downstream projects as dirty.
            switch (context.DownStreamType) {
                case DownStreamType.Direct:
                    MarkDirty(visualStudioProjects, dirtyProjects);
                    break;
                case DownStreamType.All:
                    MarkDirtyAll(visualStudioProjects, dirtyProjects);
                    break;
            }

            // Get all projects that are either visualStudio projects and dirty, or not visualStudio projects. Or say, skipped the unchanged csproj projects.
            IEnumerable<IDependencyRef> filteredProjects = visualStudioProjects.Where(x => (x as VisualStudioProject)?.IsDirty != false).ToList();
            logger.Info("Changed projects:");

            foreach (IDependencyRef project in filteredProjects) {
                logger.Info(string.Concat("* ", project.Name));
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

        private void Validate(List<VisualStudioProject> visualStudioProjects) {
            var query = visualStudioProjects.GroupBy(x => x.ProjectGuid)
                .Where(g => g.Count() > 1)
                .Select(y => new { Element = y.Key, Counter = y.Count() })
                .ToList();

            if (query.Any()) {
                throw new Exception("There are projects with duplicate project GUIDs: " + string.Join(", ", query.Select(s => s.Element)));
            }
        }

        private void IncludeInBuild(ParseResult result, ProjectConfigurationInSolution configuration, string absolutePath, IEnumerable<VisualStudioProject> visualStudioProjects) {
            foreach (VisualStudioProject project in visualStudioProjects) {
                if (string.Equals(project.Path, absolutePath, StringComparison.OrdinalIgnoreCase)) {
                    project.IncludeInBuild = true;
                    project.SolutionFile = result.SolutionFile;
                    project.BuildConfiguration = new BuildConfiguration(/*Debug or Release*/configuration.ConfigurationName, /*x86 etc*/configuration.PlatformName );
                    
                    break;
                }
            }
        }

        public XElement CreateProjectDocument(Project project) {
            BuildElementVisitor visitor = new ParallelBuildVisitor();
            project.Accept(visitor);

            return visitor.GetDocument();
        }

        internal static void SaveBuildProject(string file, XElement projectDocument) {
            File.WriteAllText(file, projectDocument.ToString(), Encoding.UTF8);
        }
    }

    internal class BuildConfiguration {
        public string ConfigurationName { get; }
        public string PlatformName { get; }

        public BuildConfiguration(string configurationName, string platformName) {
            ConfigurationName = configurationName;
            PlatformName = platformName;
        }
    }
}
