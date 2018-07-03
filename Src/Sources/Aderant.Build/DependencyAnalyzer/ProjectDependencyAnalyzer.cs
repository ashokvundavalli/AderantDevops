using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.Commands;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Utilities;

namespace Aderant.Build.DependencyAnalyzer {
    internal class ProjectDependencyAnalyzer {
        private const string BuildT4Task = "Build.T4Task";
        private readonly List<string> excludedPatterns = new List<string>();
        private readonly IFileSystem2 fileSystem;
        private readonly CSharpProjectLoader loader;
        private readonly List<string> parseErrors = new List<string>();
        private readonly Dictionary<Guid, VisualStudioProject> projectByGuidCache = new Dictionary<Guid, VisualStudioProject>();
        private readonly ICollection<string> solutionFileFilters = new List<string>();
        private readonly TextTemplateAnalyzer textTemplateAnalyzer;

        public ProjectDependencyAnalyzer(CSharpProjectLoader loader, TextTemplateAnalyzer textTemplateAnalyzer, IFileSystem2 fileSystem) {
            this.loader = loader;
            this.textTemplateAnalyzer = textTemplateAnalyzer;
            this.fileSystem = fileSystem;

            textTemplateAnalyzer.AddExclusionPattern("__");
        }

        /// <summary>
        /// Add an exclusion pattern.
        /// </summary>
        /// <param name="pattern">The pattern to exclude</param>
        public void AddExclusionPattern(string pattern) {
            if (!excludedPatterns.Contains(pattern)) {
                excludedPatterns.Add(pattern);
            }
        }

        public void AddSolutionFileNameFilter(string pattern) {
            if (!solutionFileFilters.Contains(pattern)) {
                solutionFileFilters.Add(pattern);
            }
        }

        /// <summary>
        /// Build the dependency graph between the projects in the root folder
        /// </summary>
        /// <param name="context">The context.</param>
        public DependencyGraph GetDependencyGraph(AnalyzerContext context) {
            // Load all the necessary project files and record their dependency relationships.
            var graph = BuildDependencyGraph(context);

            //GraphVisitor visitor = new GraphVisitor(fileSystem.Root);
            //visitor.BeginVisit(graph);
           
            return new DependencyGraph(graph);
        }

        private string GetSolutionRootForProject(ICollection<string> ceilingDirectories, ICollection<string> solutionFileNameFilter, string visualStudioProjectPath) {
            FileInfo info = new FileInfo(visualStudioProjectPath);
            DirectoryInfo directory = info.Directory;
            FileInfo[] solutionFiles;

            while (true) {
                if (directory != null) {
                    solutionFiles = directory.GetFiles("*.sln").Where(s => s.FullName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).ToArray();

                    if (solutionFiles.Length > 0) {
                        bool continueSearch = false;

                        foreach (string solutionFilter in solutionFileNameFilter) {
                            foreach (FileInfo file in solutionFiles) {
                                if (file.FullName.IndexOf(solutionFilter, StringComparison.OrdinalIgnoreCase) >= 0) {
                                    continueSearch = true;
                                    break;
                                }
                            }

                            if (continueSearch) {
                                break;
                            }
                        }

                        if (continueSearch) {
                            directory = directory.Parent;
                            continue;
                        }

                        break;
                    }

                    if (ceilingDirectories.Any(dir => directory.FullName.Equals(dir, StringComparison.OrdinalIgnoreCase))) {
                        break;
                    }

                    directory = directory.Parent;
                }
            }

            if (solutionFiles != null && solutionFiles.Length > 0) {
                return solutionFiles[0].DirectoryName;
            }

            return null;
        }

        private void LoadProjects(AnalyzerContext context) {

            List<string> projectFiles = new List<string>();

            foreach (string directory in context.Directories) {
                projectFiles.AddRange(fileSystem.GetFiles(directory, "*.csproj", true));
            }

            var files = context.PendingChanges;

            foreach (string projectFile in projectFiles) {
                VisualStudioProject studioProject;
                if (loader.TryParse(context.Directories, projectFile, out studioProject)) {
                    if (!projectByGuidCache.ContainsKey(studioProject.ProjectGuid)) {

                        // check if this proj contains needed files
                        if (files != null) {
                            foreach (var file in files) {

                                if (studioProject.ContainsFile(file.Path)) {
                                    // found one
                                    this.projectByGuidCache[studioProject.ProjectGuid] = studioProject;
                                    break;
                                }
                            }

                            break;
                        }

                    } else {

                    }

                    studioProject.SolutionRoot = GetSolutionRootForProject(context.Directories, solutionFileFilters, studioProject.Path);
                } else {
                    parseErrors.Add(projectFile);
                }
            }

            context.ProjectFiles = projectFiles;
        }

        /// <summary>
        /// Walk through all the project files, along with a possible changeset files list, to determine which projects are needed
        /// to be built, and their order.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private List<IDependencyRef> BuildDependencyGraph(AnalyzerContext context) {
            List<string> projectFiles = new List<string>();

            foreach (string directory in context.Directories) {
                projectFiles.AddRange(fileSystem.GetFiles(directory, "*.csproj", true));
            }

            List<IDependencyRef> graph = new List<IDependencyRef>();

            foreach (string projectFile in projectFiles) {
                VisualStudioProject studioProject;
                if (loader.TryParse(context.Directories, projectFile, out studioProject)) {
                    if (!projectByGuidCache.ContainsKey(studioProject.ProjectGuid)) {

                        // check if this proj contains needed files, make a mark
                        if (context.PendingChanges != null) {
                            foreach (var file in context.PendingChanges) {
                                var fullPath = Path.Combine(context.ModulesDirectory, file.Path);
                                if (studioProject.ContainsFile(fullPath)) {
                                    // found one
                                    studioProject.IsDirty = true;
                                    break;
                                }
                            }

                        }

                        // the project may need to be added regardless of changed or not
                        this.projectByGuidCache[studioProject.ProjectGuid] = studioProject;
                    }

                    graph.Add(studioProject);

                    studioProject.SolutionRoot = GetSolutionRootForProject(context.Directories, solutionFileFilters, studioProject.Path);
                } else {
                    parseErrors.Add(projectFile);
                }
            }

            AddDependenciesFromDependencySystem(graph);
            AddDependenciesFromTextTemplates(graph);

            List<VisualStudioProject> studioProjects = graph.OfType<VisualStudioProject>().ToList();

            ResolveProjectReferences(studioProjects);
            AddInitializeAndCompletionNodes(graph, studioProjects);

            foreach (IDependencyRef dependency in graph) {
                AddSyntheticProjectToProjectDependencies(dependency, graph);

                if (dependency is VisualStudioProject) {
                    graph = ProcessVisualStudioProject((VisualStudioProject)dependency, graph, studioProjects);
                }

                if (dependency is ExpertModule) {
                    graph = ProcessExpertModule((ExpertModule)dependency, graph, studioProjects);
                }
            }

            return graph;
        }

        internal List<IDependencyRef> ProcessVisualStudioProject(VisualStudioProject studioProject, List<IDependencyRef> graph, List<VisualStudioProject> projectsInBuild) {
            if (studioProject == null) {
                throw new ArgumentNullException(nameof(studioProject));
            }

            List<ExpertModule> modules = graph.OfType<ExpertModule>().ToList();
            ExpertModule solutionRootDependency = modules.SingleOrDefault(x => x.Match(studioProject.SolutionDirectoryName));

            if (solutionRootDependency != null) {
                studioProject.AddDependency(solutionRootDependency);
            }

            foreach (IDependencyRef dependency in studioProject.DependsOn) {
                IDependencyRef target;
                ModuleRef moduleRef = dependency as ModuleRef;

                if (moduleRef != null) {
                    ExpertModule moduleTarget = modules.SingleOrDefault(x => x.Match(dependency.Name));
                    target = moduleTarget;
                } else {
                    bool isAmbiguous = false;

                    Debug.Assert(dependency != null);

                    try {
                        target = projectsInBuild.SingleOrDefault(x => string.Equals(x.AssemblyName, dependency.Name, StringComparison.OrdinalIgnoreCase));
                    } catch (InvalidOperationException) {
                        isAmbiguous = true;
                        target = null;
                    }

                    if (target == null) {
                        target = GetDependentProjectByGuid(dependency, projectsInBuild);
                    }

                    if (target == null && isAmbiguous) {
                        target = GetDependentProjectByHintPath(studioProject, dependency, projectsInBuild);

                        if (target == null) {
                            var items = projectsInBuild.Where(x => string.Equals(x.AssemblyName, dependency.Name, StringComparison.OrdinalIgnoreCase));
                            string paths = string.Join(", ", items.Select(p => p.Path));
                            throw new AmbiguousNameException($"The assembly name {dependency.Name} is ambiguous as the following projects specify the same name: " + paths);
                        }
                    }
                }

                if (target != null) {
                    studioProject.AddDependency(target);
                }
            }

            return graph;
        }

        private VisualStudioProject GetDependentProjectByHintPath(VisualStudioProject studioProject, IDependencyRef dependency, List<VisualStudioProject> projects) {
            AssemblyRef assemblyRef = dependency as AssemblyRef;
            if (assemblyRef != null) {
                // Attempts to find a match based on the hint path of the reference
                var directoryOfProject = Path.GetDirectoryName(studioProject.Path);

                string directoryNameOfReference = Path.GetDirectoryName(assemblyRef.ReferenceHintPath);

                foreach (var project in projects) {
                    var makeRelative = PathUtility.MakeRelative(directoryOfProject, project.SolutionRoot);

                    if (directoryNameOfReference != null && directoryNameOfReference.StartsWith(makeRelative)) {
                        if (string.Equals(project.AssemblyName, dependency.Name, StringComparison.OrdinalIgnoreCase)) {
                            return project;
                        }
                    }
                }
            }

            return null;
        }

        internal List<IDependencyRef> ProcessExpertModule(ExpertModule expertModule, List<IDependencyRef> graph) {
            return ProcessExpertModule(expertModule, graph, graph.OfType<VisualStudioProject>().ToList());
        }

        private List<IDependencyRef> ProcessExpertModule(ExpertModule expertModule, List<IDependencyRef> graph, List<VisualStudioProject> projectVertices) {
            if (expertModule == null) {
                throw new ArgumentNullException(nameof(expertModule));
            }

            foreach (IDependencyRef dependencyRef in expertModule.DependsOn) {
                IDependencyRef target = graph.OfType<ExpertModule>().SingleOrDefault(x => x.Match(dependencyRef.Name)) ?? (IDependencyRef)projectVertices.SingleOrDefault(x => string.Equals(x.AssemblyName, dependencyRef.Name, StringComparison.OrdinalIgnoreCase));

                if (target != null) {
                    ((IDependencyRef)expertModule).AddDependency(target);
                }
            }

            return graph;
        }

        private void AddInitializeAndCompletionNodes(List<IDependencyRef> graph, List<VisualStudioProject> projects) {
            IEnumerable<IGrouping<string, VisualStudioProject>> grouping = projects.GroupBy(g => g.SolutionRoot);

            foreach (IGrouping<string, VisualStudioProject> level in grouping) {
                IDependencyRef initializeNode;
                string solutionDirectoryName = Path.GetFileName(level.Key);
                // Create a new node that represents the start of a directory
                graph.Add(initializeNode = new DirectoryNode(solutionDirectoryName, false));

                // Create a new node that represents the completion of a directory
                DirectoryNode completionNode = new DirectoryNode(solutionDirectoryName, true);
                completionNode.AddDependency(initializeNode);

                foreach (VisualStudioProject project in projects.Where(p => p.SolutionRoot == level.Key)) {
                    project.AddDependency(initializeNode);
                    
                    completionNode.AddDependency(project);
                }
            }
        }

        private void ResolveProjectReferences(List<VisualStudioProject> projectVertices) {
            IEnumerable<ProjectRef> projectReferences = projectVertices.SelectMany(s => s.DependsOn).OfType<ProjectRef>();

            foreach (ProjectRef projectReference in projectReferences) {
                if (!projectReference.Resolve(projectVertices)) {
                    // TODO: Can't find the project?!
                }
            }
        }

        private void AddSyntheticProjectToProjectDependencies(IDependencyRef dependencyRef, List<IDependencyRef> graph) {
            VisualStudioProject project = dependencyRef as VisualStudioProject;

            if (dependencyRef.Name != BuildT4Task) {
                if (project != null && project.DependsOn.Any(d => d.Name == BuildT4Task)) {
                    if (project.SolutionDirectoryName == BuildT4Task) {
                        return;
                    }

                    IEnumerable<VisualStudioProject> projects = graph.OfType<VisualStudioProject>().Where(s => s.SolutionDirectoryName == BuildT4Task);

                    foreach (var p in projects) {
                        project.AddDependency(p);
                    }
                }

                ExpertModule module = dependencyRef as ExpertModule;
                if (module != null) {
                    if (module.DependsOn.Any(d => d.Name == BuildT4Task)) {
                        IEnumerable<VisualStudioProject> projects = graph.OfType<VisualStudioProject>().Where(s => s.SolutionDirectoryName == BuildT4Task);
                        foreach (VisualStudioProject p in projects) {
                            module.AddDependency(p);
                        }
                    }
                }
            }
        }

        private static IDependencyRef GetDependentProjectByGuid(IDependencyRef dep, List<VisualStudioProject> projectVertices) {
            // Lots of mistakes everywhere! E.g. ProjectReference blocks that refer to the wrong assembly name.
            // For example here the name of the reference is "Rates" but the actual assembly name is "ExpertRates". 
            // Because of these errors we need to lookup by GUID, which in turn is not guaranteed to be unique as people can
            // copy/paste project files.
            //    <ProjectReference Include="..\..\Src\Rates\Rates.csproj">
            //      <Project>{DA34956E-42A2-4023-9A46-EA33CDB2D144}</Project>
            //      <Name>Rates</Name>
            //    </ProjectReference>
            IDependencyRef target = null;

            ProjectRef projectReference = dep as ProjectRef;
            if (projectReference != null) {
                try {
                    VisualStudioProject visualStudioProject = projectVertices.SingleOrDefault(s => Equals(s.ProjectGuid, projectReference.ProjectGuid));
                    target = visualStudioProject;
                } catch (InvalidOperationException) {
                    throw new InvalidOperationException("There is more than one project in the build with the project GUID: " + projectReference.ProjectGuid);
                }
            }

            return target;
        }

        private void AddDependenciesFromDependencySystem(List<IDependencyRef> graph) {
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (IDependencyRef item in graph) {
                VisualStudioProject project = item as VisualStudioProject;

                if (project != null && !string.IsNullOrWhiteSpace(project.SolutionRoot)) {
                    visited.Add(project.SolutionRoot);
                    DependencyManifest manifest;

                    try {
                        manifest = DependencyManifest.LoadDirectlyFromModule(project.SolutionRoot);
                    } catch (InvalidOperationException) {

                        continue;
                    }

                    ExpertModule module = ExpertModule.Create(project.SolutionRoot, new[] { Path.GetFileName(project.SolutionRoot) }, new DependencyManifest(project.Name, new XDocument()));
                    graph.Add(module);

                    AddDependenciesToProjects(project.SolutionRoot, graph, module, manifest);
                }
            }
        }

        private void AddDependenciesToProjects(string solutionRoot, IEnumerable<IDependencyRef> graph, ExpertModule expertModule, DependencyManifest dependencies) {
            List<VisualStudioProject> projects = graph.OfType<VisualStudioProject>().Where(p => string.Equals(p.SolutionRoot, solutionRoot, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (VisualStudioProject project in projects) {
                project.AddDependency(expertModule);

                AddDependenciesFromManifest(project, dependencies);
            }
        }

        // Adds dependencies from manifests.
        private void AddDependenciesFromManifest(VisualStudioProject project, DependencyManifest dependencies) {
            var allowed =
                new string[] {
                    "Libraries.SoftwareFactory",
                    "Build.T4"
                };

            foreach (ExpertModule module in dependencies.ReferencedModules) {
                if (allowed.Contains(module.Name)) {
                    project.AddDependency(module);
                }
            }
        }

        private void AddDependenciesFromTextTemplates(ICollection<IDependencyRef> graphVertices) {
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IDependencyRef item in graphVertices) {
                VisualStudioProject project = item as VisualStudioProject;

                if (project != null && visited.Add(project.SolutionRoot)) {
                    List<TextTemplateAnalysisResult> list = textTemplateAnalyzer.GetDependencies(project.SolutionRoot);
                    AddDependenciesToProjects(project.SolutionRoot, graphVertices, list);
                }
            }
        }

        private void AddDependenciesToProjects(string solutionRoot, ICollection<IDependencyRef> collection, List<TextTemplateAnalysisResult> templates) {
            IEnumerable<VisualStudioProject> projects = collection.OfType<VisualStudioProject>().Where(p => string.Equals(p.SolutionRoot, solutionRoot, StringComparison.OrdinalIgnoreCase));

            foreach (VisualStudioProject project in projects) {
                foreach (TextTemplateAnalysisResult template in templates) {
                    //if (!template.AssemblyReferences.Any() && !template.IsDomainModelDslTemplate && !template.IsServiceDslTemplate) {
                    //    continue;
                    //}

                    if (project.ContainsFile(template.TemplateFile)) {
                        foreach (string dependencyAssemblyReference in template.AssemblyReferences) {
                            project.AddDependency(new AssemblyRef(dependencyAssemblyReference));
                        }
                    }
                }
            }

            ExpertModule module = collection.OfType<ExpertModule>().SingleOrDefault(s => s.Match(Path.GetFileName(solutionRoot)));
            if (module != null) {
                foreach (TextTemplateAnalysisResult template in templates) {
                    if (!template.AssemblyReferences.Any() && !template.IsDomainModelDslTemplate && !template.IsServiceDslTemplate) {
                        continue;
                    }

                    foreach (string reference in template.AssemblyReferences) {
                        module.AddDependency(new AssemblyRef(reference));
                    }
                }
            }
        }

   
    }

    internal class GraphVisitor : GraphVisitorBase {
        private readonly string moduleRoot;

        public GraphVisitor(string moduleRoot) {
            this.moduleRoot = moduleRoot;
        }

        public override void Visit(ExpertModule expertModule, StreamWriter outputFile) {
            Console.WriteLine(String.Format("{0, -60} - {1}", expertModule.Name, expertModule.GetType().Name));
            Console.WriteLine($"|   |--- {expertModule.DependsOn.Count} dependencies");

            outputFile.WriteLine(String.Format("{0, -60} - {1}", $"{expertModule.Name} ({expertModule.DependsOn.Count})", expertModule.GetType().Name));

            foreach (IDependencyRef dependencyRef in expertModule.DependsOn) {
                outputFile.WriteLine($"|   |---{dependencyRef.Name}");
            }
        }

        public void Visit(ModuleRef moduleRef, StreamWriter outputFile) {
            Console.WriteLine("I am a ModuleRef");
        }

        public void Visit(AssemblyRef assemblyRef, StreamWriter outputFile) {
            Console.WriteLine("I am an AssemblyRef");
        }

        public void Visit(VisualStudioProject visualStudioProject, StreamWriter outputFile) {
            Console.WriteLine(String.Format("{0, -60} - {1}", visualStudioProject.Name, visualStudioProject.GetType().Name));
            Console.WriteLine($"|   |--- {visualStudioProject.DependsOn.Count} dependencies");

            var v = visualStudioProject.IsDirty ? "*" : "";
            outputFile.WriteLine(String.Format("{0, -60} - {1}", $"{visualStudioProject.Name} {v} ({visualStudioProject.DependsOn.Count}) ", visualStudioProject.GetType().Name));
            foreach (var dependencyRef in visualStudioProject.DependsOn) {
                outputFile.WriteLine($"|   |---{dependencyRef.Name}");
            }
        }

        public void Visit(ProjectRef projectRef, StreamWriter outputFile) {
            Console.WriteLine(String.Format("{0, -60} - {1}", projectRef.Name, projectRef.GetType().Name));
            Console.WriteLine($"|   |---");
        }

        public void BeginVisit(TopologicalSort<IDependencyRef> graph) {
            string treeFile = Path.Combine(moduleRoot, "DependencyGraph.txt");

            //using (StreamWriter outputFile = new StreamWriter(treeFile, false)) {
            //    foreach (IDependencyRef item in graph) {
            //        item.Accept(this, outputFile);
            //    }
            //}

            Console.WriteLine($"To view the full dependencies graph, see: {treeFile}");
        }
    }

}
