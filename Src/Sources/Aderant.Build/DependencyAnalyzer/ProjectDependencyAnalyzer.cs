using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer.Model;

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
        public List<IDependencyRef> GetDependencyOrder(AnalyzerContext context) {

            // Load all the necessary project files and record their dependency relationships.
            TopologicalSort<IDependencyRef> graph = GetDependencyGraph(context);

            GraphVisitor visitor = new GraphVisitor(fileSystem.Root);
            visitor.BeginVisit(graph);

            // Solve the build order.
            var ordered = GetDependencyOrderFromGraph(graph);
            return ordered;
        }

        public List<List<IDependencyRef>> GetBuildGroups(IEnumerable<IDependencyRef> projects) {
            return GetBuildGroupsInternal(projects);
        }

        private List<IDependencyRef> GetDependencyOrderFromGraph(TopologicalSort<IDependencyRef> graph) {
            Queue<IDependencyRef> queue;

            if (!graph.Sort(out queue)) {
                DetectCircularDependencies(queue);
                throw new CircularDependencyException(queue.Select(s => s.Name));
            }

            return queue.ToList();
        }

        internal List<IDependencyRef> DetectCircularDependencies(Queue<IDependencyRef> queue) {
            IEnumerable<string> moduleNames = queue.Select(s => s.Name);
            List<IDependencyRef> dependencies = new List<IDependencyRef>();

            foreach (IDependencyRef module in queue) {
                List<IDependencyRef> dependencyReferences = module.DependsOn.Select(x => x).Where(y => moduleNames.Contains(y.Name)).ToList();

                if (dependencyReferences.Any()) {
                    dependencies.Add(
                        new ExpertModule(module.Name) {
                            DependsOn = dependencyReferences
                        });
                }
            }

            return dependencies;
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
                projectFiles.AddRange(fileSystem.GetFiles(directory, "*.csproj", true, true));
            }

            var files = context.Files;

            foreach (string projectFile in projectFiles) {
                VisualStudioProject studioProject;
                if (loader.TryParse(context.Directories, projectFile, out studioProject)) {
                    if (!projectByGuidCache.ContainsKey(studioProject.ProjectGuid)) {

                        // check if this proj contains needed files
                        if (files != null) {
                            foreach (var file in files) {

                                if (studioProject.ContainsFile(file)) {
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
        private TopologicalSort<IDependencyRef> GetDependencyGraph(AnalyzerContext context) {
            List<string> projectFiles = new List<string>();

            foreach (string directory in context.Directories) {
                projectFiles.AddRange(fileSystem.GetFiles(directory, "*.csproj", true, true));
            }

            TopologicalSort<IDependencyRef> graph = new TopologicalSort<IDependencyRef>();

            foreach (string projectFile in projectFiles) {
                VisualStudioProject studioProject;
                if (loader.TryParse(context.Directories, projectFile, out studioProject)) {
                    if (!projectByGuidCache.ContainsKey(studioProject.ProjectGuid)) {

                        // check if this proj contains needed files, make a mark
                        if (context.Files != null) {
                            foreach (var file in context.Files) {
                                var fullPath = Path.Combine(context.ModulesDirectory, file);
                                if (studioProject.ContainsFile(fullPath)) {
                                    // found one
                                    studioProject.IsDirty = true;
                                    break;
                                }
                            }

                        }

                        // the project may need to be added regardless of changed or not
                        this.projectByGuidCache[studioProject.ProjectGuid] = studioProject;

                    } else {

                    }

                    graph.Edge(studioProject);

                    studioProject.SolutionRoot = GetSolutionRootForProject(context.Directories, solutionFileFilters, studioProject.Path);
                } else {
                    parseErrors.Add(projectFile);
                }
            }

            AddDependenciesFromDependencySystem(graph);
            AddDependenciesFromTextTemplates(graph.Vertices);

            List<VisualStudioProject> projectVertices = graph.Vertices.OfType<VisualStudioProject>().ToList();

            ResolveProjectReferences(projectVertices);
            AddInitializeAndCompletionNodes(graph, projectVertices);

            foreach (IDependencyRef dependency in graph.Vertices) {
                AddSyntheticProjectToProjectDependencies(dependency, graph);

                if (dependency is VisualStudioProject) {
                    graph = ProcessVisualStudioProject((VisualStudioProject)dependency, graph, projectVertices);
                }

                if (dependency is ExpertModule) {
                    graph = ProcessExpertModule((ExpertModule)dependency, graph, projectVertices);
                }
            }

            return graph;
        }

        internal TopologicalSort<IDependencyRef> ProcessVisualStudioProject(VisualStudioProject studioProject, TopologicalSort<IDependencyRef> graph) {
            return ProcessVisualStudioProject(studioProject, graph, graph.Vertices.OfType<VisualStudioProject>().ToList());
        }

        private TopologicalSort<IDependencyRef> ProcessVisualStudioProject(VisualStudioProject studioProject, TopologicalSort<IDependencyRef> graph, List<VisualStudioProject> projectsInBuild) {
            if (studioProject == null) {
                throw new ArgumentNullException(nameof(studioProject));
            }

            List<ExpertModule> moduleVertices = graph.Vertices.OfType<ExpertModule>().ToList();
            ExpertModule solutionRootDependency = moduleVertices.SingleOrDefault(x => x.Match(studioProject.SolutionDirectoryName));

            if (solutionRootDependency != null) {
                graph.Edge(studioProject, solutionRootDependency);
            }

            foreach (IDependencyRef dependency in studioProject.DependsOn) {
                IDependencyRef target;
                ModuleRef moduleRef = dependency as ModuleRef;

                if (moduleRef != null) {
                    ExpertModule moduleTarget = moduleVertices.SingleOrDefault(x => x.Match(dependency.Name));
                    target = moduleTarget;
                } else {
                    bool isAmbiguous = false;

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

                if (target == null) {
                    if (dependency is DirectoryNode) {

                    }
                }

                if (target != null) {
                    graph.Edge(studioProject, target);
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

        internal TopologicalSort<IDependencyRef> ProcessExpertModule(ExpertModule expertModule, TopologicalSort<IDependencyRef> graph) {
            return ProcessExpertModule(expertModule, graph, graph.Vertices.OfType<VisualStudioProject>().ToList());
        }

        private TopologicalSort<IDependencyRef> ProcessExpertModule(ExpertModule expertModule, TopologicalSort<IDependencyRef> graph, List<VisualStudioProject> projectVertices) {
            if (expertModule == null) {
                throw new ArgumentNullException(nameof(expertModule));
            }

            foreach (IDependencyRef dependencyRef in expertModule.DependsOn) {
                IDependencyRef target = graph.Vertices.OfType<ExpertModule>().ToList().SingleOrDefault(x => x.Match(dependencyRef.Name)) ?? (IDependencyRef)projectVertices.SingleOrDefault(x => string.Equals(x.AssemblyName, dependencyRef.Name, StringComparison.OrdinalIgnoreCase));

                if (target != null) {
                    graph.Edge(expertModule, target);
                }
            }

            return graph;
        }

        private void AddInitializeAndCompletionNodes(TopologicalSort<IDependencyRef> graph, List<VisualStudioProject> projects) {
            IEnumerable<IGrouping<string, VisualStudioProject>> grouping = projects.GroupBy(g => g.SolutionRoot);

            foreach (IGrouping<string, VisualStudioProject> level in grouping) {
                IDependencyRef initializeNode;
                string solutionDirectoryName = Path.GetFileName(level.Key);
                // Create a new node that represents the start of a directory
                graph.Edge(initializeNode = new DirectoryNode(solutionDirectoryName, false));

                // Create a new node that represents the completion of a directory
                DirectoryNode completionNode = new DirectoryNode(solutionDirectoryName, true);
                completionNode.AddDependency(initializeNode);
                graph.Edge(completionNode, initializeNode);

                foreach (VisualStudioProject project in projects.Where(p => p.SolutionRoot == level.Key)) {
                    project.AddDependency(initializeNode);
                    graph.Edge(project, initializeNode);

                    completionNode.AddDependency(project);
                    graph.Edge(completionNode, project);
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

        private void AddSyntheticProjectToProjectDependencies(IDependencyRef dependencyRef, TopologicalSort<IDependencyRef> graph) {
            VisualStudioProject project = dependencyRef as VisualStudioProject;

            if (dependencyRef.Name != BuildT4Task) {
                if (project != null && project.DependsOn.Any(d => d.Name == BuildT4Task)) {
                    if (project.SolutionDirectoryName == BuildT4Task) {
                        return;
                    }

                    IEnumerable<VisualStudioProject> projects = graph.Vertices.OfType<VisualStudioProject>().Where(s => s.SolutionDirectoryName == BuildT4Task);

                    foreach (var p in projects) {
                        project.DependsOn.Add(p);
                    }
                }

                ExpertModule module = dependencyRef as ExpertModule;
                if (module != null) {
                    if (module.DependsOn.Any(d => d.Name == BuildT4Task)) {
                        IEnumerable<VisualStudioProject> projects = graph.Vertices.OfType<VisualStudioProject>().Where(s => s.SolutionDirectoryName == BuildT4Task);
                        foreach (VisualStudioProject p in projects) {
                            module.DependsOn.Add(p);
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

        private void AddDependenciesFromDependencySystem(TopologicalSort<IDependencyRef> graph) {
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<IDependencyRef> vertices = graph.Vertices.ToList();

            foreach (IDependencyRef item in vertices) {
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
                    graph.Edge(module);

                    AddDependenciesToProjects(project.SolutionRoot, graph, module, manifest);
                }
            }
        }

        // Looks like it gets paket.template names?
        private IEnumerable<string> GetAdditionalComponents(string solutionRoot) {
            yield return Path.GetFileName(solutionRoot);

            foreach (string file in fileSystem.GetFiles(solutionRoot, "*.template", false)) {
                using (Stream stream = fileSystem.OpenFile(file)) {
                    using (StreamReader reader = new StreamReader(stream)) {
                        string line;

                        while ((line = reader.ReadLine()) != null) {
                            line = line.TrimStart();

                            if (line.StartsWith("id ", StringComparison.OrdinalIgnoreCase)) {
                                string alias = line.Substring(3);

                                yield return alias;
                            }
                        }
                    }
                }
            }
        }

        private void AddDependenciesToProjects(string solutionRoot, TopologicalSort<IDependencyRef> graph, ExpertModule expertModule, DependencyManifest dependencies) {
            List<VisualStudioProject> projects = graph.Vertices.OfType<VisualStudioProject>().Where(p => string.Equals(p.SolutionRoot, solutionRoot, StringComparison.OrdinalIgnoreCase)).ToList();

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
                    List<TextTemplateAssemblyInfo> list = textTemplateAnalyzer.GetDependencies(project.SolutionRoot);
                    AddDependenciesToProjects(project.SolutionRoot, graphVertices, list);
                }
            }
        }

        private void AddDependenciesToProjects(string solutionRoot, ICollection<IDependencyRef> collection, List<TextTemplateAssemblyInfo> templates) {
            IEnumerable<VisualStudioProject> projects = collection.OfType<VisualStudioProject>().Where(p => string.Equals(p.SolutionRoot, solutionRoot, StringComparison.OrdinalIgnoreCase));

            foreach (VisualStudioProject project in projects) {
                foreach (TextTemplateAssemblyInfo template in templates) {
                    //if (!template.AssemblyReferences.Any() && !template.IsDomainModelDslTemplate && !template.IsServiceDslTemplate) {
                    //    continue;
                    //}

                    if (project.ContainsFile(template.TemplateFile)) {
                        foreach (string dependencyAssemblyReference in template.AssemblyReferences) {
                            project.DependsOn.Add(new AssemblyRef(dependencyAssemblyReference));
                        }
                    }
                }
            }

            ExpertModule module = collection.OfType<ExpertModule>().SingleOrDefault(s => s.Match(Path.GetFileName(solutionRoot)));
            if (module != null) {
                foreach (TextTemplateAssemblyInfo template in templates) {
                    if (!template.AssemblyReferences.Any() && !template.IsDomainModelDslTemplate && !template.IsServiceDslTemplate) {
                        continue;
                    }

                    foreach (string reference in template.AssemblyReferences) {
                        module.DependsOn.Add(new AssemblyRef(reference));
                    }
                }
            }
        }

        private static List<List<IDependencyRef>> GetBuildGroupsInternal(IEnumerable<IDependencyRef> sortedQueue) {
            // Now find critical path...
            // What we do here is iterate the sorted list looking for elements with no dependencies. These are the zero level modules.
            // Then we iterate again and check if the module depends on any of the zero level modules but not on anything else. These are the
            // level 1 elements. Then we iterate again and check if the module depends on any of the 0 or 1 level modules etc.
            // This places modules into levels which allows for maximum parallelism based on dependency.
            IDictionary<int, HashSet<IDependencyRef>> levels = new Dictionary<int, HashSet<IDependencyRef>>();

            Queue<IDependencyRef> projects = new Queue<IDependencyRef>(sortedQueue);

            int i = 0;
            while (projects.Count > 0) {
                IDependencyRef project = projects.Peek();

                if (!levels.ContainsKey(i)) {
                    levels[i] = new HashSet<IDependencyRef>();
                }

                bool add = true;

                if (project.DependsOn != null) {
                    var levelSet = levels[i];
                    foreach (var item in levelSet) {
                        if (project.DependsOn.Contains(item)) {
                            add = false;
                        }
                    }
                }

                if (add) {
                    levels[i].Add(project);
                    projects.Dequeue();
                } else {
                    i++;
                }
            }

            List<List<IDependencyRef>> groups = new List<List<IDependencyRef>>();
            foreach (KeyValuePair<int, HashSet<IDependencyRef>> pair in levels) {
                groups.Add(new List<IDependencyRef>(levels[pair.Key]));
            }

            return groups;
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

            using (StreamWriter outputFile = new StreamWriter(treeFile, false)) {
                foreach (IDependencyRef item in graph.Vertices) {
                    item.Accept(this, outputFile);
                }
            }

            Console.WriteLine($"To view the full dependencies graph, see: {treeFile}");
        }
    }

}
