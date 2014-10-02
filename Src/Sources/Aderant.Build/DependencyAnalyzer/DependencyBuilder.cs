using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyAnalyzer {
    
    internal class DependencyBuilder {
        private const string mgraphRootNamespace = "http://graphml.graphdrawing.org/xmlns";
        private const string dgmlRootNamespace = "http://schemas.microsoft.com/vs/2009/dgml";

        private readonly IModuleProvider moduleProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyBuilder"/> class.
        /// </summary>
        /// <param name="provider">The provider.</param>
        public DependencyBuilder(IModuleProvider provider) {
            this.moduleProvider = provider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyBuilder" /> class.
        /// </summary>
        /// <param name="branchRootOrModulePath">The branch root or module path.</param>
        /// <exception cref="System.ArgumentException">modulePath must be a rooted path;branchRootOrModulePath</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public DependencyBuilder(string branchRootOrModulePath) {
            if (!Path.IsPathRooted(branchRootOrModulePath)) {
                throw new ArgumentException("modulePath must be a rooted path", "branchRootOrModulePath");
            }

            this.moduleProvider = new WorkspaceModuleProvider(branchRootOrModulePath);
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
            var modules = moduleProvider.GetAll().Select(m => m.Name);

            IEnumerable<XElement> moduleElements = moduleProvider.ProductManifest.Root.Descendants("Modules").Descendants().ToList();

            Debug.Assert(moduleElements.Count() > 1);

            IDictionary<string, ExpertModule> providers = new Dictionary<string, ExpertModule>();
            IList<ModuleDependency> dependencies = new List<ModuleDependency>();

            foreach (string module in modules) {
                XDocument manifest;
                if (!moduleProvider.TryGetDependencyManifest(module, out manifest)) {
                    continue;
                }

                if (ExpertModule.IsNonProductModule(ExpertModule.GetModuleType(module))) {
                    continue;
                }

                IEnumerable<XElement> references = manifest.Descendants("ReferencedModule").ToList();

                if (!references.Any()) {
                    XElement productManifestEntry = moduleElements.FirstOrDefault(n => n.Attribute("Name").Value.Equals(module, StringComparison.OrdinalIgnoreCase));
                    providers[module.ToUpperInvariant()] = new ExpertModule(productManifestEntry);

                    var dependency = new ModuleDependency() {
                        Consumer = new ExpertModule {
                            Name = module
                        }
                    };

                    dependency.Provider = dependency.Consumer;

                    dependencies.Add(dependency);
                } else {
                    foreach (XElement dependencyElement in references) {
                        string referencedModule = dependencyElement.Attribute("Name").Value.ToUpperInvariant();

                        if (!includeThirdParty && ExpertModule.GetModuleType(referencedModule) == ModuleType.ThirdParty) {
                            continue;
                        }

                        dependencies.Add(new ModuleDependency() {
                            Consumer = new ExpertModule {
                                Name = module
                            },
                            Provider = CreateExpertModule(moduleElements, referencedModule, providers),
                        });
                    }
                }

                // If we have nothing that depends on us, we need to create a dependency entry for ourselves
                // This covers cases like Internal.Validation on which nothing depends.
                ExpertModule expertModule = CreateExpertModule(moduleElements, module, providers);
                if (expertModule != null && !ExpertModule.IsNonProductModule(expertModule.ModuleType)) {
                    var dependency = new ModuleDependency() {
                        Provider = expertModule,Consumer = expertModule
                    };

                    if (!dependencies.Contains(dependency)) {
                        dependencies.Add(dependency);
                    }
                }
            }

            // Now we need to add a dependency for each module that points to itself. 
            // If we have the case where all of a given module's dependencies are external to the branch, 
            // the restriction filter will exclude not only the external dependencies but also the module itself
            IEnumerable<ModuleDependency> nodes = dependencies.Select(m => m.Consumer)
                                                              .Distinct()
                                                              .Select(m => new ModuleDependency {
                                                                  Consumer = m,
                                                                  Provider = m
                                                              });

            return dependencies.Union(nodes.OrderBy(x => x.Consumer.Name)).ToList();
        }

        private static ExpertModule CreateExpertModule(IEnumerable<XElement> moduleElements, string moduleName, IDictionary<string, ExpertModule> providers) {
            XElement productManifestEntry = moduleElements.FirstOrDefault(n => n.Attribute("Name").Value.Equals(moduleName, StringComparison.OrdinalIgnoreCase));

            ExpertModule module;
            if (!providers.TryGetValue(moduleName, out module) && productManifestEntry != null) {
                module = new ExpertModule(productManifestEntry);
                providers.Add(moduleName, module);
            }

            if (module == null && ExpertModule.IsNonProductModule(ExpertModule.GetModuleType(moduleName))) {
                return null;
            }

            if (module == null) {
                throw new InvalidOperationException("Could not find Expert Manifest entry for: " + moduleName);
            }

            return module;
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
                    new XElement(XName.Get("graphml", mgraphRootNamespace),
                                 new XElement(XName.Get("graph", mgraphRootNamespace),
                                              new object[] {
                                                  new XAttribute(XName.Get("id"), "G"),
                                                  new XAttribute(XName.Get("edgedefault"), "directed"),
                                                  new XAttribute(XName.Get("parse.nodes"), "8"),
                                                  new XAttribute(XName.Get("parse.edges"), "7"),
                                                  new XAttribute(XName.Get("parse.order"), "nodesfirst"),
                                                  new XAttribute(XName.Get("parse.nodeids"), "free"),
                                                  new XAttribute(XName.Get("parse.edgeids"), "free"),
                                                  allModules.Select(module => new XElement(XName.Get("node", mgraphRootNamespace), new object[] {new XAttribute(XName.Get("id"), module.Name), new XAttribute(XName.Get("desc"), allModules.IndexOf(module))})),
                                                  allDependencies.Select(dependency => new XElement(XName.Get("edge", mgraphRootNamespace), new object[] {new XAttribute(XName.Get("id"), allDependencies.IndexOf(dependency)), new XAttribute(XName.Get("source"), allModules.IndexOf(dependency.Consumer)), new XAttribute(XName.Get("target"), allModules.IndexOf(dependency.Provider))}))
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
                    new XElement(XName.Get("DirectedGraph", dgmlRootNamespace),
                                 new XElement(XName.Get("Nodes", dgmlRootNamespace),
                                              new object[] {
                                                  allModules.Select(module => new XElement(XName.Get("Node", dgmlRootNamespace),
                                                                                           new object[] {
                                                                                               new XAttribute(XName.Get("Id"), allModules.IndexOf(module)),
                                                                                               new XAttribute(XName.Get("Label"), module.Name)
                                                                                           })),
                                                  ((includeBuilds ? GetTree(restrictToModulesInBranch) : new Build[0]).Select((item, index) =>
                                                                                                                              new XElement(XName.Get("Node", dgmlRootNamespace),
                                                                                                                                           new object[] {
                                                                                                                                               new XAttribute(XName.Get("Id"), string.Format("Build{0}", index)),
                                                                                                                                               new XAttribute(XName.Get("Group"), "Expanded"),
                                                                                                                                               new XAttribute(XName.Get("Label"), string.Format("Build Level {0}", index))
                                                                                                                                           })))
                                              }
                                     ),
                                 new XElement(XName.Get("Links", dgmlRootNamespace),
                                              new object[] {
                                                  allDependencies.Select(dependency => new XElement(XName.Get("Link", dgmlRootNamespace), new object[] {
                                                      new XAttribute(XName.Get("Consumer"), allModules.IndexOf(dependency.Consumer)),
                                                      new XAttribute(XName.Get("Provider"), allModules.IndexOf(dependency.Provider))
                                                  })),
                                                  ((includeBuilds ? GetTree(restrictToModulesInBranch) : new Build[0]).Select((build, levelIndex) => build.Modules.Select(item =>
                                                                                                                                                                          new XElement(XName.Get("Link", dgmlRootNamespace), new object[] {
                                                                                                                                                                              new XAttribute(XName.Get("Source"), string.Format("Build{0}", levelIndex)),
                                                                                                                                                                              new XAttribute(XName.Get("Target"), allModules.IndexOf(item)),
                                                                                                                                                                              new XAttribute(XName.Get("Category"), "Contains")
                                                                                                                                                                          }))
                                                  ))
                                              }
                                     ),
                                 new XElement(XName.Get("Categories", dgmlRootNamespace),
                                              new object[] {
                                                  new XElement(XName.Get("Category", dgmlRootNamespace), new object[] {
                                                      new XAttribute(XName.Get("Id"), "Contains"),
                                                      new XAttribute(XName.Get("Label"), "Contans"),
                                                      new XAttribute(XName.Get("CanBeDataDriven"), "False"),
                                                      new XAttribute(XName.Get("CanLinkedNodesBeDataDriven"), "True"),
                                                      new XAttribute(XName.Get("IncomingActionLabel"), "Contained By"),
                                                      new XAttribute(XName.Get("IsContainment"), "True"),
                                                      new XAttribute(XName.Get("OutgoingActionLabel"), "Contains"),
                                                  })
                                              }),
                                 new XElement(XName.Get("Properties", dgmlRootNamespace), new object[] {
                                     new XElement("Property", dgmlRootNamespace,
                                                  new XAttribute("Id", "Label"),
                                                  new XAttribute("Label", "Label"),
                                                  new XAttribute("Description", "Displayable label of an Annotatable object"),
                                                  new XAttribute("DataType", "System.String")
                                                                                                   ),
                                     new XElement("Property", dgmlRootNamespace,
                                                  new XAttribute("Id", "CanBeDataDriven"),
                                                  new XAttribute("Label", "CanBeDataDriven"),
                                                  new XAttribute("Description", "CanBeDataDriven"),
                                                  new XAttribute("DataType", "System.Boolean")
                                                                                                   ),
                                     new XElement("Property", dgmlRootNamespace,
                                                  new XAttribute("Id", "CanLinkedNodesBeDataDriven"),
                                                  new XAttribute("Label", "CanLinkedNodesBeDataDriven"),
                                                  new XAttribute("Description", "CanLinkedNodesBeDataDriven"),
                                                  new XAttribute("DataType", "System.Boolean")
                                                                                                   ),
                                     new XElement("Property", dgmlRootNamespace,
                                                  new XAttribute("Id", "GraphDirection"),
                                                  new XAttribute("DataType", "Microsoft.VisualStudio.Progression.Layout.GraphDirection")
                                                                                                   ),
                                     new XElement("Property", dgmlRootNamespace,
                                                  new XAttribute("Id", "Group"),
                                                  new XAttribute("Label", "Group"),
                                                  new XAttribute("Description", "Group"),
                                                  new XAttribute("DataType", "Microsoft.VisualStudio.Progression.GraphModel.GroupStyle")
                                                                                                   ),
                                     new XElement("Property", dgmlRootNamespace,
                                                  new XAttribute("Id", "IncomingActionLabel"),
                                                  new XAttribute("Label", "IncomingActionLabel"),
                                                  new XAttribute("Description", "IncomingActionLabel"),
                                                  new XAttribute("DataType", "System.String")
                                                                                                   ),
                                     new XElement("Property", dgmlRootNamespace,
                                                  new XAttribute("Id", "IsContainment"),
                                                  new XAttribute("DataType", "System.Boolean")
                                                                                                   ),
                                     new XElement("Property", dgmlRootNamespace,
                                                  new XAttribute("Id", "Layout"),
                                                  new XAttribute("DataType", "System.String")
                                                                                                   ),
                                     new XElement("Property", dgmlRootNamespace,
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

                if (moduleProvider.IsAvailable(module.Provider.Name)) {
                    if (module.Provider.ModuleType != ModuleType.ThirdParty && !module.Provider.Equals(module.Consumer)) {
                        sort.Edge(module.Consumer, module.Provider);
                    } else {
                        // No dependencies within this branch -- detached edge
                        sort.Edge(module.Consumer);
                    }
                } else {
                    sort.Edge(module.Consumer);
                }
            }

            Queue<ExpertModule> sortedQueue;
            if (!sort.Sort(out sortedQueue)) {
                throw new CircularDependencyException("There is a circular dependency between the following modules: " + string.Join(", ", sortedQueue.Select(s => s.Name).ToArray()));
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

            return levels.Select(level => new Build {
                Modules = level.Value,
                Order = level.Key
            });
        }
    }
}