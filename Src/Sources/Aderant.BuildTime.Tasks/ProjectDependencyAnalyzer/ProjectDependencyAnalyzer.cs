using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.BuildTime.Tasks.Sequencer;
using Microsoft.Build.Construction;

namespace Aderant.BuildTime.Tasks.ProjectDependencyAnalyzer {
    internal class ProjectDependencyAnalyzer {
        private readonly CSharpProjectLoader loader;
        private readonly TextTemplateAnalyzer textTemplateAnalyzer;
        private readonly IFileSystem2 fileSystem;
        private List<string> excludedPatterns = new List<string>();
        private ICollection<string> solutionFileFilters = new List<string>();
        private List<string> parseErrors = new List<string>();
        private Dictionary<Guid, VisualStudioProject> projectByGuidCache = new Dictionary<Guid, VisualStudioProject>();
        private const string buildT4Task = "Build.T4Task";

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
            TopologicalSort<IDependencyRef> graph = GetDependencyGraph(context);

            GraphVisitor vistior = new GraphVisitor(fileSystem.Root);
            vistior.BeginVisit(graph);

            return GetDependencyOrderFromGraph(graph);
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
                List<IDependencyRef> dependencyReferences = module.Dependencies.Select(x => x).Where(y => moduleNames.Contains(y.Name)).ToList();

                if (dependencyReferences.Any()) {
                    dependencies.Add(new ExpertModule { Name = module.Name, DependsOn = dependencyReferences});
                }
            }

            if (dependencies.Any()) {
                Console.WriteLine("Detected circular references. Potential suspects:");

                foreach (IDependencyRef dependency in dependencies) {
                    Console.WriteLine(dependency.Name);

                    foreach (var item in dependency.Dependencies) {
                        Console.WriteLine($"\t{item.Name}");
                    }
                }
            } else {
                Console.WriteLine("Unable to identify circular references.");
            }

            return dependencies;
        }

        private string GetSolutionRootForProject(ICollection<string> ceilingDirectories, ICollection<string> solutionFileNameFilter, string visualStudioProjectPath) {
            FileInfo info = new FileInfo(visualStudioProjectPath);
            DirectoryInfo directory = info.Directory;
            FileInfo[] solutionFiles = null;

            while (true) {
                if (directory != null) {
                    solutionFiles = directory.GetFiles("*.sln").Where(s => s.FullName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).ToArray();

                    if (solutionFiles.Length > 0) {
                        bool continueSearch = false;

                        foreach (var solutionFilter in solutionFileNameFilter) {
                            foreach (var file in solutionFiles) {
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

        private TopologicalSort<IDependencyRef> GetDependencyGraph(AnalyzerContext context) {
            List<string> projects = new List<string>();

            foreach (string directory in context.Directories) {
                projects.AddRange(fileSystem.GetFiles(directory, "*.csproj", true, true));
            }

            IEnumerable<string> projectFiles = projects.Where(f => !excludedPatterns.Any(s => f.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0));

            TopologicalSort<IDependencyRef> graph = new TopologicalSort<IDependencyRef>();

            foreach (string projectFile in projectFiles) {
                string currentProjectFile = projectFile;

                VisualStudioProject studioProject;
                if (loader.TryParse(context.Directories, currentProjectFile, out studioProject)) {

                    if (!projectByGuidCache.ContainsKey(studioProject.ProjectGuid)) {
                        this.projectByGuidCache[studioProject.ProjectGuid] = studioProject;
                    } else {
                        
                    }

                    graph.Edge(studioProject);

                    string solutionRoot = GetSolutionRootForProject(context.Directories, solutionFileFilters, studioProject.Path);
                    studioProject.SolutionRoot = solutionRoot;
                } else {
                    parseErrors.Add(currentProjectFile);
                }
            }

            // We don't want to add any dependencies from the Dependency System.
            //AddDependenciesFromDependencySystem(graph);

            AddDependenciesFromTextTemplates(graph.Vertices);

            List<ExpertModule> moduleVertices = graph.Vertices.OfType<ExpertModule>().ToList();
            List<VisualStudioProject> projectVertices = graph.Vertices.OfType<VisualStudioProject>().ToList();

            ResolveProjectReferences(projectVertices);

            foreach (IDependencyRef v in graph.Vertices) {
                AddSyntheticProjectToProjectDependencies(v, graph);

                VisualStudioProject studioProject = v as VisualStudioProject;

                if (studioProject != null) {

                    var solutionRootDependency = moduleVertices.SingleOrDefault(x => x.Match(studioProject.SolutionDirectoryName));
                    if (solutionRootDependency == null) {
                        //continue;
                    } else {
                        graph.Edge(v, solutionRootDependency);
                    }

                    if (studioProject.Dependencies == null) {
                        continue;
                    }

                    foreach (IDependencyRef dep in studioProject.Dependencies) {
                        ModuleRef moduleRef = dep as ModuleRef;

                        IDependencyRef target = null;

                        if (moduleRef != null) {
                            var moduleTarget = moduleVertices.SingleOrDefault(x => x.Match(dep.Name));

                            target = moduleTarget;
                        } else {
                            target = projectVertices.SingleOrDefault(x => string.Equals(x.AssemblyName, dep.Name, StringComparison.OrdinalIgnoreCase));

                            if (target == null) {
                                target = GetDependentProjectByGuid(dep, projectVertices);
                            }
                        }

                        if (target != null) {
                            graph.Edge(v, target);

                            TraceGraph(graph);
                        }
                    }
                }

                var m = v as ExpertModule;
                if (m != null) {
                    foreach (IDependencyRef dependencyRef in m.DependsOn) {
                        IDependencyRef target = moduleVertices.SingleOrDefault(x => x.Match(dependencyRef.Name));
                        if (target == null) {
                            target = projectVertices.SingleOrDefault(x => string.Equals(x.AssemblyName, dependencyRef.Name, StringComparison.OrdinalIgnoreCase));
                        }

                        if (target != null) {
                            if (target.Name.Contains("Customization")) {
                                continue;
                            }

                            if (target.Name.Contains("ClientMatter")) {
                                continue;
                            }

                            if (target.Name.Contains("AccountsPayable")) {
                                continue;
                            }

                            if (target.Name.Contains("Services.Applications.FirmControl")) {
                                continue;
                            }

                            graph.Edge(m, target);

                            TraceGraph(graph);
                        }
                    }
                }
            }

            PatchInSoftwareFactory(graph);

            return graph;
        }

        private void ResolveProjectReferences(List<VisualStudioProject> projectVertices) {
            IEnumerable<ProjectRef> projectReferences = projectVertices.SelectMany(s => s.Dependencies).OfType<ProjectRef>();

            foreach (ProjectRef projectReference in projectReferences) {
                projectReference.Resolve(projectVertices);
            }
        }

        private void AddSyntheticProjectToProjectDependencies(IDependencyRef dependencyRef, TopologicalSort<IDependencyRef> graph) {
            if (dependencyRef.Name == "SDK.Database") {
                
            }
            VisualStudioProject project = dependencyRef as VisualStudioProject;

            if (dependencyRef.Name != buildT4Task) {
                if (project != null && project.Dependencies.Any(d => d.Name == buildT4Task)) {
                    if (project.SolutionDirectoryName == buildT4Task) {
                        return;
                    }

                    IEnumerable<VisualStudioProject> projects = graph.Vertices.OfType<VisualStudioProject>().Where(s => s.SolutionDirectoryName == buildT4Task);

                    foreach (var p in projects) {
                        project.Dependencies.Add(p);
                    }
                }

                var module = dependencyRef as ExpertModule;
                if (module != null) {
                    if (module.DependsOn.Any(d => d.Name == buildT4Task)) {
                        IEnumerable<VisualStudioProject> projects = graph.Vertices.OfType<VisualStudioProject>().Where(s => s.SolutionDirectoryName == buildT4Task);
                        foreach (var p in projects) {
                            module.DependsOn.Add(p);
                        }
                    }
                }
            }
        }

        private void PatchInSoftwareFactory(TopologicalSort<IDependencyRef> graph) {
            AddFakeEndNode("Libraries.SoftwareFactory", graph);
            //AddFakeEndNode(buildT4Task, graph);
        }

        private static void AddFakeEndNode(string name, TopologicalSort<IDependencyRef> graph) {
            var end = new ProjectRef(name + ".End");

            foreach (var project in graph.Vertices.ToList()) {
                VisualStudioProject studioProject = project as VisualStudioProject;
                if (studioProject != null && studioProject.SolutionDirectoryName == name) {
                    graph.Edge(end, studioProject);
                    continue;
                }

                if (project.Name == name) {
                    graph.Edge(end, studioProject);
                    continue;
                }

                graph.Edge(project, end);
            }
        }

        private void TraceGraph(TopologicalSort<IDependencyRef> graph) {
            if (TraceCircularDependency) {
                TopologicalSort<IDependencyRef> clone = graph.Clone();

                Queue<IDependencyRef> queue;
                if (!clone.Sort(out queue)) {
                    throw new CircularDependencyException(queue.Select(s => s.Name));
                }
            }
        }

        /// <summary>
        /// Instructs the analyzer to sort the dependency graph on each edge relationship that is added.
        /// This makes it easier to pinpoint a circular dependency.
        /// </summary>
        public bool TraceCircularDependency { get; set; }

        private static IDependencyRef GetDependentProjectByGuid(IDependencyRef dep, List<VisualStudioProject> projectVertices) {
            // Lots of mistakes everywhere! E.g. ProjectReference blocks that refer to the wrong assembly name.
            // For example here the name of the reference is "Rates" but the actual assembly name is "ExpertRates". 
            // Because of these errors we need to lookup by GUID, which in turn is not guarnteed to be unique as people can
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

            foreach (var item in vertices) {
                VisualStudioProject project = item as VisualStudioProject;

                if (project != null && visited.Add(project.SolutionRoot)) {
                    string[] strings = GetAdditionalComponents(project.SolutionRoot).Where(s => s != "Aderant.StoredProcedures").Where(s => s != "Aderant.Libraries.Models").ToArray();

                    try {
                        DependencyManifest manifest = DependencyManifest.LoadFromModule(project.SolutionRoot);

                        ExpertModule module = ExpertModule.Create(project.SolutionRoot, strings, manifest);

                        graph.Edge(module);

                        AddDependenciesToProjects(project.SolutionRoot, graph, module, manifest);
                    } catch {
                        // Ignore.
                    }
                }
            }
        }

        // Looks like it gets paket.template names?

        private IEnumerable<string> GetAdditionalComponents(string solutionRoot) {
            yield return Path.GetFileName(solutionRoot);

            foreach (string file in fileSystem.GetFiles(solutionRoot, "*.template", false)) {
                using (Stream stream = fileSystem.OpenFile(file)) {
                    using (var reader = new StreamReader(stream)) {
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

        // Adds dependencies from manifests.
        private void AddDependenciesToProjects(string solutionRoot, TopologicalSort<IDependencyRef> graph, ExpertModule expertModule, DependencyManifest dependencies) {
            var projects = graph.Vertices.OfType<VisualStudioProject>().Where(p => string.Equals(p.SolutionRoot, solutionRoot, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (VisualStudioProject project in projects) {
                project.Dependencies.Add(expertModule);

                foreach (ExpertModule module in dependencies.ReferencedModules) {
                    project.Dependencies.Add(new ModuleRef(module));
                }
            }
        }

        private void AddDependenciesFromTextTemplates(ICollection<IDependencyRef> graphVertices) {
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in graphVertices) {
                VisualStudioProject project = item as VisualStudioProject;

                if (project != null && visited.Add(project.SolutionRoot)) {
                    List<TextTemplateAssemblyInfo> list = textTemplateAnalyzer.GetDependencies(project.SolutionRoot);

                    //if (list.Any() && project.Dependencies.All(x => x.Name != buildT4Task)) {
                    //    project.Dependencies.Add(new ExpertModule{
                    //        Name = buildT4Task
                    //    });
                    //}

                    AddDependenciesToProjects(project.SolutionRoot, graphVertices, list);
                }
            }
        }

        private void AddDependenciesToProjects(string solutionRoot, ICollection<IDependencyRef> collection, List<TextTemplateAssemblyInfo> templates) {
            var projects = collection.OfType<VisualStudioProject>().Where(p => string.Equals(p.SolutionRoot, solutionRoot, StringComparison.OrdinalIgnoreCase));

            foreach (var project in projects) {
                foreach (var template in templates) {
                    //if (!template.AssemblyReferences.Any() && !template.IsDomainModelDslTemplate && !template.IsServiceDslTemplate) {
                    //    continue;
                    //}

                    if (project.ContainsFile(template.TemplateFile)) {
                        foreach (string dependencyAssemblyReference in template.AssemblyReferences) {
                            project.Dependencies.Add(new AssemblyRef(dependencyAssemblyReference, DependencyType.TextTemplate));
                        }
                    }
                }
            }

            ExpertModule module = collection.OfType<ExpertModule>().SingleOrDefault(s => s.Match(Path.GetFileName(solutionRoot)));
            if (module != null) {
                foreach (var template in templates) {
                    if (!template.AssemblyReferences.Any() && !template.IsDomainModelDslTemplate && !template.IsServiceDslTemplate) {
                        continue;
                    }

                    foreach (var reference in template.AssemblyReferences) {
                        module.DependsOn.Add(new AssemblyRef(reference, DependencyType.TextTemplate));
                    }
                }
            }
        }

        private static List<List<IDependencyRef>> GetBuildGroupsInternal(IEnumerable<IDependencyRef> sortedQueue) {
            // Now find critical path...
            // What we do here is iterate the sorted list looking for elements with no dependencies. These are the zero level modules.
            // Then we iterate again and check if the module dependends on any of the zero level modules but not on anything else. These are the
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

                VisualStudioProject studioProject = project as VisualStudioProject;

                if (studioProject?.Dependencies != null) {
                    if (studioProject.Path == @"C:\agent\_work\4\s\AccountsPayable\Src\Aderant.AccountsPayable.Administration\Aderant.AccountsPayable.Administration.csproj")
                    {

                    }

                    foreach (var dependency in studioProject.Dependencies) {
                        if (studioProject.Path == @"C:\agent\_work\4\s\AccountsPayable\Src\Aderant.AccountsPayable.Administration\Aderant.AccountsPayable.Administration.csproj") {
                            bool any = levels[i].Any(p => studioProject.Dependencies.Contains(p, DependencyEqualityComparer.Default));
                            bool any2 = levels[i].Any(
                                p => {

                                    foreach (var dd in studioProject.Dependencies) {
                                        if (dd.Equals(p)) {
                                            return true;
                                        }
                                    }

                                    return false;

                                }
                            );
                        }

                        if (levels[i].Any(p => string.Equals(p.Name, dependency.Name, StringComparison.OrdinalIgnoreCase))) {
                            add = false;
                        }
                    }
                }

                var m = project as ExpertModule;
                if (m != null) {
                    foreach (var dependency in m.DependsOn)
                    {
                        if (levels[i].Any(p => string.Equals(p.Name, dependency.Name, StringComparison.OrdinalIgnoreCase)))
                        {
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

            var groups = new List<List<IDependencyRef>>();
            foreach (KeyValuePair<int, HashSet<IDependencyRef>> pair in levels) {
                groups.Add(new List<IDependencyRef>(levels[pair.Key]));
            }

            return groups;
        }
    }

    internal class DependencyEqualityComparer : IEqualityComparer<IDependencyRef> {

        public static DependencyEqualityComparer Default = new DependencyEqualityComparer();

        private DependencyEqualityComparer() {
            
        }

        public bool Equals(IDependencyRef x, IDependencyRef y) {
            return x.Equals(y);
        }

        public int GetHashCode(IDependencyRef obj) {
            return obj.GetHashCode();
        }
    }

    internal enum DependencyType {
        Unknown = -1,
        TextTemplate
    }

    [DebuggerDisplay("{" + nameof(Name) + "}")]
    internal class ModuleRef : IDependencyRef {
        private readonly ExpertModule module;

        public ModuleRef(ExpertModule module) {
            this.module = module;
        }

        public string Name => module.Name;
        public ICollection<IDependencyRef> Dependencies => null;

        public void Accept(GraphVisitorBase visitor, StreamWriter outputFile) {
            (visitor as GraphVisitor).Visit(this, outputFile);

            foreach (IDependencyRef dep in module.DependsOn) {
                dep.Accept(visitor, outputFile);
            }
        }

        public bool Equals(IDependencyRef @ref) {
            var moduleRef = @ref as ModuleRef;
            if (moduleRef != null && string.Equals(Name, moduleRef.Name, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            return false;
        }
    }

    internal class GraphVisitor : GraphVisitorBase {
        private readonly string moduleRoot;

        public GraphVisitor(string moduleRoot) {
            this.moduleRoot = moduleRoot;
        }

        public override void Visit(ExpertModule expertModule, StreamWriter outputFile) {
            Console.WriteLine(String.Format("|   {0, -60} - {1}", expertModule.Name, expertModule.GetType().Name));
            Console.WriteLine($"|       |--- {expertModule.DependsOn.Count} dependencies");

            outputFile.WriteLine(String.Format("|   {0, -60} - {1}", $"{expertModule.Name} ({expertModule.DependsOn.Count})", expertModule.GetType().Name));

            foreach (IDependencyRef dependencyRef in expertModule.DependsOn) {
                outputFile.WriteLine($"|       |---{dependencyRef.Name}");
            }
        }

        public void Visit(ModuleRef moduleRef, StreamWriter outputFile) {
            Console.WriteLine("I am a ModuleRef");
        }

        public void Visit(AssemblyRef assemblyRef, StreamWriter outputFile) {
            Console.WriteLine("I am a AssemblyRef");
        }

        public void Visit(VisualStudioProject visualStudioProject, StreamWriter outputFile) {
            Console.WriteLine(String.Format("|   {0, -60} - {1}", visualStudioProject.Name, visualStudioProject.GetType().Name));
            Console.WriteLine($"|       |--- {visualStudioProject.Dependencies.Count} dependencies");

            outputFile.WriteLine(String.Format("|   {0, -60} - {1}", $"{visualStudioProject.Name} ({visualStudioProject.Dependencies.Count})", visualStudioProject.GetType().Name));
            foreach (var dependencyRef in visualStudioProject.Dependencies) {
                outputFile.WriteLine($"|       |---{dependencyRef.Name}");
            }
        }

        public void Visit(ProjectRef projectRef, StreamWriter outputFile) {
            Console.WriteLine(String.Format("|   {0, -60} - {1}", projectRef.Name, projectRef.GetType().Name));
            Console.WriteLine($"|       |---");
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

    public interface ISolutionFileParser {
        ParseResult Parse(string solutionFile);
    }

    public class ParseResult {
        public string SoluitionFile { get; internal set; }
        public IReadOnlyDictionary<string, ProjectInSolution> ProjectsByGuid { get; internal set; }
        public IReadOnlyList<ProjectInSolution> ProjectsInOrder { get; internal set; }
        public IReadOnlyList<SolutionConfigurationInSolution> SolutionConfigurations { get; internal set; }
    }

    public class SolutionFileParser : ISolutionFileParser {
        public ParseResult Parse(string solutionFile) {
            SolutionFile file = SolutionFile.Parse(solutionFile);

            return new ParseResult {
                SoluitionFile = solutionFile,
                ProjectsByGuid = file.ProjectsByGuid,
                ProjectsInOrder = file.ProjectsInOrder,
                SolutionConfigurations = file.SolutionConfigurations
            };
        }
    }
}