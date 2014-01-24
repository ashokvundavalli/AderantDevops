using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DependencyAnalyzer.Providers {
    /// <summary>
    /// A <see cref="IModuleProvider"/> which returns branch information from the current Team Foundation workspace.
    /// </summary>
    public class WorkspaceModuleProvider : IModuleProvider {
        private const string DependencyManifest = "DependencyManifest.xml";
        private readonly string moduleDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkspaceModuleProvider"/> class.
        /// </summary>
        /// <param name="moduleDirectory">The module directory.</param>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public WorkspaceModuleProvider(string moduleDirectory) {
            string path = moduleDirectory;

            if (!moduleDirectory.Split(Path.DirectorySeparatorChar).Last().Equals("Modules", StringComparison.OrdinalIgnoreCase)) {
                path = Path.Combine(moduleDirectory, "Modules");
            }

            if (!Directory.Exists(path)) {
                throw new DirectoryNotFoundException(path);
            }

            this.moduleDirectory = path;
            this.Branch = PathHelper.GetBranch(moduleDirectory);
            
            this.ProductManifestPath = PathHelper.Aggregate(this.moduleDirectory, PathHelper.PathToProductManifest);
            this.ProductManifest = XDocument.Load(PathHelper.Aggregate(this.moduleDirectory, PathHelper.PathToProductManifest));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkspaceModuleProvider"/> class.
        /// </summary>
        /// <param name="productProductManifest">The product product manifest.</param>
        /// <param name="moduleDirectory">The module directory.</param>
        public WorkspaceModuleProvider(XDocument productProductManifest, string moduleDirectory) {
            ProductManifest = productProductManifest;
            this.moduleDirectory = moduleDirectory;
        }

        /// <summary>
        /// Gets the product manifest.
        /// </summary>
        /// <value>
        /// The product manifest.
        /// </value>
        public XDocument ProductManifest {
            get;
            private set;
        }

        /// <summary>
        /// Gets the product manifest path.
        /// </summary>
        /// <value>
        /// The product manifest path.
        /// </value>
        public string ProductManifestPath {
            get;
            private set;
        }

        /// <summary>
        /// Gets the two part branch name
        /// </summary>
        /// <value>
        /// The branch.
        /// </value>
        public string Branch {
            get;
            private set;
        }

        /// <summary>
        /// Gets the distinct complete list of available modules and those referenced in Dependency Manifests.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ExpertModule> GetAll() {
            HashSet<ExpertModule> branchModules = new HashSet<ExpertModule>();

            var modules = from moduleElement in ProductManifest.Root.Descendants("Module")
                          where moduleElement.Attribute("Name") != null 
                          select new ExpertModule {
                              Name = moduleElement.Attribute("Name").Value.ToPascalCase()
                          };

            foreach (string directory in Directory.GetDirectories(moduleDirectory)) {
                XDocument manifest;
                if (!TryGetDependencyManifest(directory, out manifest)) {
                    continue;
                }

                branchModules.Add(new ExpertModule {
                    Name = directory
                });

                foreach (XElement element in manifest.Descendants("ReferencedModule")) {
                    XAttribute attribute = element.Attribute("Name");
                    if (attribute != null)
                        branchModules.Add(new ExpertModule() {
                            Name = attribute.Value.ToPascalCase()
                        });
                }
            }

            return modules.Union(branchModules);
        }

        /// <summary>
        /// Tries to get the Dependency Manifest document from the given module.  
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="manifest">The manifest.</param>
        /// <returns></returns>
        public bool TryGetDependencyManifest(string moduleName, out XDocument manifest) {
            string dependencyManifest = PathHelper.Aggregate(moduleDirectory, moduleName, "Build", DependencyManifest);

            if (File.Exists(dependencyManifest)) {
                manifest = XDocument.Load(dependencyManifest);
                return true;
            }

            manifest = null;
            return false;
        }

        /// <summary>
        /// Tries to the get the path to the dependency manifest for a given module.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="manifestPath">The manifest path.</param>
        /// <returns></returns>
        public bool TryGetDependencyManifestPath(string moduleName, out string manifestPath) {
            string dependencyManifest = PathHelper.Aggregate(moduleDirectory, moduleName, "Build", DependencyManifest);

            if (File.Exists(dependencyManifest)) {
                manifestPath = dependencyManifest;
                return true;
            }
            
            manifestPath = null;
            return false;
        }

        /// <summary>
        /// Determines whether the specified module is available to the current branch.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns>
        ///   <c>true</c> if the specified module name is available; otherwise, <c>false</c>.
        /// </returns>
        public bool IsAvailable(string moduleName) {
            return File.Exists(PathHelper.Aggregate(moduleDirectory, moduleName, "Build", "TFSBuild.proj"));
        }
    }
}