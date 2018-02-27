using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Xml.Linq;
using Aderant.Build.Providers;
using Aderant.Build.Tasks;

namespace Aderant.Build.DependencyAnalyzer {
    public interface IDependencyBuilder {
        /// <summary>
        /// Gets the distinct list of modules in the module folder or referenced in Dependency Manifests.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ExpertModule> GetAllModules();

        IEnumerable<ModuleDependency> GetModuleDependencies();

        IEnumerable<ModuleDependency> GetModuleDependencies(bool includeThirdParty);

        /// <summary>
        /// Builds the graph document for the dependency tree.
        /// </summary>
        /// <returns></returns>
        XDocument BuildMGraphDocument();

        XDocument BuildDgmlDocument(bool includeBuilds, bool restrictToModulesInBranch);

        /// <summary>
        /// Gets the dependency tree.
        /// </summary>
        /// <param name="restrictToModulesInBranch">if set to <c>true</c> restricts the analysis to modules in the current branch.</param>
        /// <returns></returns>
        /// <exception cref="CircularDependencyException">Whe a a circular dependency between following modules is detected</exception>
        IEnumerable<Build> GetTree(bool restrictToModulesInBranch);

        ICollection<ExpertModule> GetDownstreamModules(ICollection<ExpertModule> modules);
    }

    public class DependencyBuilder : IDependencyBuilder {
        private const string mgraphRootNamespace = "http://graphml.graphdrawing.org/xmlns";
        private const string dgmlRootNamespace = "http://schemas.microsoft.com/vs/2009/dgml";

        private readonly IModuleProvider moduleProvider;
        private List<string> exclusions = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyBuilder"/> class.
        /// </summary>
        /// <param name="provider">The provider.</param>
        public DependencyBuilder(IModuleProvider provider) {
            moduleProvider = provider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyBuilder" /> class.
        /// </summary>
        /// <param name="branchRootOrModulePath">The branch root or module path.</param>
        /// <exception cref="System.ArgumentException">modulePath must be a rooted path;branchRootOrModulePath</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public DependencyBuilder(string branchRootOrModulePath) {
            if (!Path.IsPathRooted(branchRootOrModulePath)) {
                throw new ArgumentException("modulePath must be a rooted path", nameof(branchRootOrModulePath));
            }

            // TODO: HACK
            //if (branchRootOrModulePath.IndexOf(@"\Modules", StringComparison.OrdinalIgnoreCase) == -1) {
            //    branchRootOrModulePath = Path.Combine(branchRootOrModulePath, "Modules");
            //}

            moduleProvider = new DependencyManifestProvider(branchRootOrModulePath);
        }

        /// <summary>
        /// Gets the distinct list of modules in the module folder or referenced in Dependency Manifests.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ExpertModule> GetAllModules() {
            return moduleProvider.GetAll();
        }

        public IEnumerable<ModuleDependency> GetModuleDependencies() {
            return GetModuleDependencies(false);
        }

        public IEnumerable<ModuleDependency> GetModuleDependencies(bool includeThirdParty) {
            var allModules = moduleProvider.GetAll().ToList();

            HashSet<ModuleDependency> dependencies = new HashSet<ModuleDependency>();

            foreach (var module in allModules) {
                if (exclusions.Any(excl => string.Equals(excl, module.Name))) {
                    continue;
                }

                DependencyManifest manifest;
                if (!moduleProvider.TryGetDependencyManifest(module.Name, out manifest)) {
                    continue;
                }

                if (!manifest.IsEnabled) {
                    continue;
                }

                if (ExpertModule.IsNonProductModule(module.ModuleType)) {
                    continue;
                }

                if (!manifest.ReferencedModules.Any()) {
                    var dependency = new ModuleDependency {
                        Consumer = new ExpertModule {
                            Name = module.Name
                        }
                    };
                    dependency.Provider = dependency.Consumer;
                    dependencies.Add(dependency);
                } else {
                    var consumer = module;

                    foreach (ExpertModule expertModule in manifest.ReferencedModules) {
                        ExpertModule provider = expertModule;

                        if (!includeThirdParty && expertModule.ModuleType == ModuleType.ThirdParty) {
                            continue;
                        }

                        ExpertModule groupOwner;
                        if (LookupGroupOrAlias(expertModule, out groupOwner)) {
                            provider = groupOwner;
                        }

                        dependencies.Add(
                            new ModuleDependency {
                                Consumer = consumer,
                                Provider = provider,
                            });
                    }
                }

                // If we have nothing that depends on us, we need to create a dependency entry for ourselves
                // This covers cases like Internal.Validation on which nothing depends.
                if (!ExpertModule.IsNonProductModule(module.ModuleType)) {
                    AddSelfPointer(dependencies, module);
                }
            }

            // Now we need to add a dependency for each module that points to itself. 
            // If we have the case where all of a given module's dependencies are external to the branch, 
            // the restriction filter will exclude not only the external dependencies but also the module itself
            var consumers = dependencies.Select(m => m.Consumer);
            foreach (var module in consumers) {
                AddSelfPointer(dependencies, module);
            }

            return dependencies.OrderBy(x => x.Consumer.Name).ToList();
        }

        // Compatibility. Here we implement a simple grouping or alias mechanism where a single module can be known by
        // many names. This gives us the ability to partition a module into smaller components 
        private bool LookupGroupOrAlias(ExpertModule expertModule, out ExpertModule container) {
            IModuleGroupingSupport groupingSupport = moduleProvider as IModuleGroupingSupport;

            if (groupingSupport != null && groupingSupport.TryGetContainer(expertModule.Name, out container)) {
                return true;
            }

            container = null;
            return false;
        }

        private static void AddSelfPointer(HashSet<ModuleDependency> dependencies, ExpertModule module) {
            // Test if we already added this module to the set
            bool add = dependencies.All(moduleDependency => !string.Equals(moduleDependency.Consumer.Name, module.Name, StringComparison.OrdinalIgnoreCase) || !string.Equals(moduleDependency.Provider.Name, module.Name, StringComparison.OrdinalIgnoreCase));

            if (add) {
                var dependency = new ModuleDependency {
                    Provider = module,
                    Consumer = module
                };
                dependencies.Add(dependency);
            }
        }

        /// <summary>
        /// Builds the graph document for the dependency tree.
        /// </summary>
        /// <returns></returns>
        public XDocument BuildMGraphDocument() {
            List<ExpertModule> allModules = new List<ExpertModule>(GetAllModules());
            List<ModuleDependency> allDependencies = new List<ModuleDependency>(GetModuleDependencies());

            XDocument document = new XDocument(
                new object[] {
                    new XElement(
                        XName.Get("graphml", mgraphRootNamespace),
                        new XElement(
                            XName.Get("graph", mgraphRootNamespace),
                            new object[] {
                                new XAttribute(XName.Get("id"), "G"),
                                new XAttribute(XName.Get("edgedefault"), "directed"),
                                new XAttribute(XName.Get("parse.nodes"), "8"),
                                new XAttribute(XName.Get("parse.edges"), "7"),
                                new XAttribute(XName.Get("parse.order"), "nodesfirst"),
                                new XAttribute(XName.Get("parse.nodeids"), "free"),
                                new XAttribute(XName.Get("parse.edgeids"), "free"),
                                allModules.Select(module => new XElement(XName.Get("node", mgraphRootNamespace), new object[] { new XAttribute(XName.Get("id"), module.Name), new XAttribute(XName.Get("desc"), allModules.IndexOf(module)) })),
                                allDependencies.Select(dependency => new XElement(XName.Get("edge", mgraphRootNamespace), new object[] { new XAttribute(XName.Get("id"), allDependencies.IndexOf(dependency)), new XAttribute(XName.Get("source"), allModules.IndexOf(dependency.Consumer)), new XAttribute(XName.Get("target"), allModules.IndexOf(dependency.Provider)) }))
                            }
                        )
                    )
                }
            );

            return document;
        }

        public XDocument BuildDgmlDocument(bool includeBuilds, bool restrictToModulesInBranch) {
            List<ExpertModule> allModules = new List<ExpertModule>(GetAllModules());
            List<ModuleDependency> allDependencies = new List<ModuleDependency>(GetModuleDependencies());

            XDocument document = new XDocument(
                new object[] {
                    new XElement(
                        XName.Get("DirectedGraph", dgmlRootNamespace),
                        new XElement(
                            XName.Get("Nodes", dgmlRootNamespace),
                            new object[] {
                                allModules.Select(
                                    module => new XElement(
                                        XName.Get("Node", dgmlRootNamespace),
                                        new object[] {
                                            new XAttribute(XName.Get("Id"), allModules.IndexOf(module)),
                                            new XAttribute(XName.Get("Label"), module.Name)
                                        })),
                                ((includeBuilds ? GetTree(restrictToModulesInBranch) : new Build[0]).Select(
                                    (item, index) =>
                                        new XElement(
                                            XName.Get("Node", dgmlRootNamespace),
                                            new object[] {
                                                new XAttribute(XName.Get("Id"), string.Format("Build{0}", index)),
                                                new XAttribute(XName.Get("Group"), "Expanded"),
                                                new XAttribute(XName.Get("Label"), string.Format("Build Level {0}", index))
                                            })))
                            }
                        ),
                        new XElement(
                            XName.Get("Links", dgmlRootNamespace),
                            new object[] {
                                CreateLinks(allDependencies, allModules),
                                ((includeBuilds ? GetTree(restrictToModulesInBranch) : new Build[0]).Select(
                                    (build, levelIndex) => build.Modules.Select(
                                        item =>
                                            new XElement(
                                                XName.Get("Link", dgmlRootNamespace),
                                                new object[] {
                                                    new XAttribute(XName.Get("Source"), string.Format("Build{0}", levelIndex)),
                                                    new XAttribute(XName.Get("Target"), allModules.IndexOf(item)),
                                                    new XAttribute(XName.Get("Category"), "Contains")
                                                }))
                                ))
                            }
                        ),
                        new XElement(
                            XName.Get("Categories", dgmlRootNamespace),
                            new object[] {
                                new XElement(
                                    XName.Get("Category", dgmlRootNamespace),
                                    new object[] {
                                        new XAttribute(XName.Get("Id"), "Contains"),
                                        new XAttribute(XName.Get("Label"), "Contans"),
                                        new XAttribute(XName.Get("CanBeDataDriven"), "False"),
                                        new XAttribute(XName.Get("CanLinkedNodesBeDataDriven"), "True"),
                                        new XAttribute(XName.Get("IncomingActionLabel"), "Contained By"),
                                        new XAttribute(XName.Get("IsContainment"), "True"),
                                        new XAttribute(XName.Get("OutgoingActionLabel"), "Contains"),
                                    })
                            }),
                        new XElement(
                            XName.Get("Properties", dgmlRootNamespace),
                            new object[] {
                                new XElement(
                                    "Property",
                                    dgmlRootNamespace,
                                    new XAttribute("Id", "Label"),
                                    new XAttribute("Label", "Label"),
                                    new XAttribute("Description", "Displayable label of an Annotatable object"),
                                    new XAttribute("DataType", "System.String")
                                ),
                                new XElement(
                                    "Property",
                                    dgmlRootNamespace,
                                    new XAttribute("Id", "CanBeDataDriven"),
                                    new XAttribute("Label", "CanBeDataDriven"),
                                    new XAttribute("Description", "CanBeDataDriven"),
                                    new XAttribute("DataType", "System.Boolean")
                                ),
                                new XElement(
                                    "Property",
                                    dgmlRootNamespace,
                                    new XAttribute("Id", "CanLinkedNodesBeDataDriven"),
                                    new XAttribute("Label", "CanLinkedNodesBeDataDriven"),
                                    new XAttribute("Description", "CanLinkedNodesBeDataDriven"),
                                    new XAttribute("DataType", "System.Boolean")
                                ),
                                new XElement(
                                    "Property",
                                    dgmlRootNamespace,
                                    new XAttribute("Id", "GraphDirection"),
                                    new XAttribute("DataType", "Microsoft.VisualStudio.Progression.Layout.GraphDirection")
                                ),
                                new XElement(
                                    "Property",
                                    dgmlRootNamespace,
                                    new XAttribute("Id", "Group"),
                                    new XAttribute("Label", "Group"),
                                    new XAttribute("Description", "Group"),
                                    new XAttribute("DataType", "Microsoft.VisualStudio.Progression.GraphModel.GroupStyle")
                                ),
                                new XElement(
                                    "Property",
                                    dgmlRootNamespace,
                                    new XAttribute("Id", "IncomingActionLabel"),
                                    new XAttribute("Label", "IncomingActionLabel"),
                                    new XAttribute("Description", "IncomingActionLabel"),
                                    new XAttribute("DataType", "System.String")
                                ),
                                new XElement(
                                    "Property",
                                    dgmlRootNamespace,
                                    new XAttribute("Id", "IsContainment"),
                                    new XAttribute("DataType", "System.Boolean")
                                ),
                                new XElement(
                                    "Property",
                                    dgmlRootNamespace,
                                    new XAttribute("Id", "Layout"),
                                    new XAttribute("DataType", "System.String")
                                ),
                                new XElement(
                                    "Property",
                                    dgmlRootNamespace,
                                    new XAttribute("Id", "OutgoingActionLabel"),
                                    new XAttribute("Label", "OutgoingActionLabel"),
                                    new XAttribute("Description", "OutgoingActionLabel"),
                                    new XAttribute("DataType", "System.String")
                                )
                            })
                    )
                }
            );

            return document;
        }

        private static IEnumerable<XElement> CreateLinks(List<ModuleDependency> allDependencies, List<ExpertModule> allModules) {
            foreach (var dependency in allDependencies) {
                if (dependency.Consumer.Equals(dependency.Provider)) {
                    continue;
                }

                yield return new XElement(
                    XName.Get("Link", dgmlRootNamespace),
                    new object[] {
                        new XAttribute(XName.Get("Source"), allModules.IndexOf(dependency.Consumer)), new XAttribute(XName.Get("Target"), allModules.IndexOf(dependency.Provider))
                    });
            }
        }

        public bool TryGetTree(bool restrictToModulesInBranch, out IEnumerable<Build> tree, out string[] conflicts) {
            conflicts = null;
            tree = null;

            try {
                tree = GetTree(restrictToModulesInBranch);
                return true;
            } catch (CircularDependencyException ex) {
                conflicts = ex.Conflicts;
                return false;
            }
        }

        /// <summary>
        /// Gets the dependency tree.
        /// </summary>
        /// <param name="restrictToModulesInBranch">if set to <c>true</c> restricts the analysis to modules in the current branch.</param>
        /// <returns></returns>
        /// <exception cref="CircularDependencyException">Whe a a circular dependency between following modules is detected</exception>
        public IEnumerable<Build> GetTree(bool restrictToModulesInBranch) {
            IEnumerable<ModuleDependency> dependencies = GetModuleDependencies();

            if (restrictToModulesInBranch) {
                dependencies = dependencies.Where(dep => dep.Branch == null || dep.Branch.Equals(moduleProvider.Branch, StringComparison.OrdinalIgnoreCase));
            }

            TopologicalSort<ExpertModule> sort = new TopologicalSort<ExpertModule>();
            foreach (ModuleDependency module in dependencies) {
                if (module.Provider.ModuleType == ModuleType.ThirdParty) {
                    continue;
                }

                // Filter by targets in our branch - if we have the target locally it means we can build it
                // so is therefore is allowed to be included in the dependency tree.
                // If doesn't exist then it must be an external module, so it just gets given to us during the build process and we don't need an edge for 
                // the module

                if (moduleProvider.IsAvailable(module.Provider.Name) == ModuleAvailability.Availabile) {
                    if (module.Provider.ModuleType != ModuleType.ThirdParty && !module.Provider.Equals(module.Consumer)) {
                        sort.Edge(module.Consumer, module.Provider);
                    } else {
                        // No dependencies within this branch -- edge
                        sort.Edge(module.Consumer);
                    }
                } else {
                    sort.Edge(module.Consumer);
                }
            }

            Queue<ExpertModule> sortedQueue;
            if (!sort.Sort(out sortedQueue)) {
                throw new CircularDependencyException(sortedQueue.Select(s => s.Name));
            }

            return GetBuildGroups(sortedQueue, dependencies);
        }

        private static IEnumerable<Build> GetBuildGroups(Queue<ExpertModule> sortedQueue, IEnumerable<ModuleDependency> dependencies) {
            // Now find critical path...
            // What we do here is iterate the sorted list looking for elements with no dependencies. These are the zero level modules.
            // Then we iterate again and check if the module dependends on any of the zero level modules but not on anything else. These are the
            // level 1 elements. Then we iterate again and check if the module depends on any of the 0 or 1 level modules etc.
            // This places modules into levels which allows for maximum parallelism based on dependency.
            IDictionary<int, HashSet<ExpertModule>> levels = new Dictionary<int, HashSet<ExpertModule>>();

            int i = 0;
            while (sortedQueue.Count > 0) {
                ExpertModule expertModule = sortedQueue.Peek();

                IEnumerable<ModuleDependency> moduleDependencies = dependencies.Where(m => m.Consumer.Equals(expertModule));

                if (!levels.ContainsKey(i)) {
                    levels[i] = new HashSet<ExpertModule>();
                }

                bool add = true;
                foreach (ModuleDependency dependency in moduleDependencies) {
                    if (levels[i].Any(module => dependency.Provider.Equals(module))) {
                        add = false;
                    }
                }

                if (add) {
                    levels[i].Add(expertModule);
                    sortedQueue.Dequeue();
                } else {
                    i++;
                }
            }

            return levels.Select(
                level => new Build {
                    Modules = level.Value,
                    Order = level.Key
                });
        }

        public ICollection<ExpertModule> GetDownstreamModules(ICollection<ExpertModule> modules) {
            List<ModuleDependency> moduleDependencies = new List<ModuleDependency>(GetModuleDependencies());

            HashSet<ExpertModule> collector = new HashSet<ExpertModule>();

            List<ExpertModule> input = modules.ToList();

            GetDependents(input, moduleDependencies, collector);

            // All going well this should equal zero! If it doesn't we have vertices with no edges.
            // var inputSet = new HashSet<ExpertModule>(modules);
            // inputSet.ExceptWith(collector);

            return collector;
        }

        private void GetDependents(List<ExpertModule> input, List<ModuleDependency> moduleDependencies, HashSet<ExpertModule> collector) {
            for (int i = input.Count - 1; i >= 0; i--) {
                ExpertModule module = input[i];

                // Add the module itself - this ensures we capture modules which are vertices 
                collector.Add(module);

                List<ExpertModule> dependents = GetDependents(module, moduleDependencies).ToList();

                foreach (ExpertModule dependent in dependents) {
                    // If the dependent is in our input list we can remove it as we will include it in the exhaustive loop
                    // Otherwise we will double process the module which is inefficient
                    input.RemoveAll(p => string.Equals(p.Name, dependent.Name, StringComparison.OrdinalIgnoreCase));

                    collector.Add(dependent);

                    // Now get the children of the children (if any)
                    GetDependents(new List<ExpertModule> { dependent }, moduleDependencies, collector);
                }
            }
        }

        private IEnumerable<ExpertModule> GetDependents(ExpertModule module, List<ModuleDependency> moduleDependencies) {
            for (int i = moduleDependencies.Count - 1; i >= 0; i--) {
                ModuleDependency dependency = moduleDependencies[i];

                if (dependency.Provider.Equals(module)) {
                    if (!dependency.Consumer.Equals(module)) {
                        moduleDependencies.RemoveAt(i);
                        yield return dependency.Consumer;
                    }
                }
            }
        }

        public void AddExclusion(string name) {
            this.exclusions.Add(name);
        }
    }
}