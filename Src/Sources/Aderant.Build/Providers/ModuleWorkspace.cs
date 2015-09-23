using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Providers {
    /// <summary>
    /// Represents an ExpertSuite developement environment . 
    /// 
    /// The ideal is to make this class the single entry point for all services required for working with Expert Suite.
    /// This class should manage the various manifest files and provide a set of dependency analysis services.
    /// 
    /// The class also talks to Team Foundation. 
    /// </summary>
    internal class ModuleWorkspace {
        //private string teamProject;
        //private string teamFoundationServerUri;
        //private IServiceProvider teamFoundationFactory;

        ///// <summary>
        ///// Initializes a new instance of the <see cref="BuildDetailPublisher"/> class.
        ///// </summary>
        ///// <param name="teamFoundationServerUri">The team foundation server URI.</param>
        ///// <param name="teamProject">The team project.</param>
        //public WorkspaceModuleProvider(string teamFoundationServerUri, string teamProject) {
        //    this.teamProject = teamProject;
        //    this.teamFoundationServerUri = teamFoundationServerUri;
        //}

        public ModuleWorkspace(string expertManifestPath) {
            IModuleProvider manifest = ExpertManifest.Load(expertManifestPath);

            DependencyAnalyzer = new DependencyBuilder(manifest);
        }

        ///// <summary>
        ///// Gets or sets the team foundation server factory.
        ///// </summary>
        ///// <value>
        ///// The team foundation factory.
        ///// </value>
        //public IServiceProvider TeamFoundationServiceFactory {

        //    get {
        //        if (teamFoundationFactory == null) {
        //            teamFoundationFactory = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(teamFoundationServerUri));
        //        }
        //        return teamFoundationFactory;
        //    }
        //    set { teamFoundationFactory = value; }
        //}

        public DependencyBuilder DependencyAnalyzer { get; private set; }

//        private readonly ExpertManifest expertManifest;
//        private readonly string moduleDirectory;

//        /// <summary>
//        /// Initializes a new instance of the <see cref="WorkspaceModuleProvider" /> class.
//        /// </summary>
//        /// <param name="moduleDirectory"></param>
//        /// <param name="expertManifest">The expert manifest.</param>
//        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
//        public WorkspaceModuleProvider(string moduleDirectory, ExpertManifest expertManifest) {
//            if (moduleDirectory.IndexOf("Modules", StringComparison.OrdinalIgnoreCase) == -1) {
//                moduleDirectory = Path.Combine(moduleDirectory, "Modules");
//            }

//            this.moduleDirectory = moduleDirectory;
//            this.expertManifest = expertManifest;

//            Branch = expertManifest.Branch;
//        }

//        /// <summary>
//        /// Gets the product manifest path.
//        /// </summary>
//        /// <value>
//        /// The product manifest path.
//        /// </value>
//        public string ProductManifestPath { get; private set; }

//        /// <summary>
//        /// Gets the two part branch name
//        /// </summary>
//        /// <value>
//        /// The branch.
//        /// </value>
//        public string Branch { get; private set; }

//        /// <summary>
//        /// Gets the distinct complete list of available modules and those referenced in Dependency Manifests.
//        /// </summary>
//        /// <returns></returns>
//        public IEnumerable<ExpertModule> GetAll() {
//            HashSet<ExpertModule> branchModules = new HashSet<ExpertModule>();

//            IEnumerable<ExpertModule> modules = expertManifest.GetAll();

//            foreach (string directory in Directory.GetDirectories(moduleDirectory)) {
//                DependencyManifest manifest;
//                if (!TryGetDependencyManifest(directory, out manifest)) {
//                    continue;
//                }

//                branchModules.Add(new ExpertModule {
//                    Name = directory
//                });

//                foreach (ExpertModule module in manifest.ReferencedModules) {
//                    branchModules.Add(module);
//                }
//            }

//            return modules.Union(branchModules);
//        }

//        /// <summary>
//        /// Tries to get the Dependency Manifest document from the given module.  
//        /// </summary>
//        /// <param name="moduleName">Name of the module.</param>
//        /// <param name="manifest">The manifest.</param>
//        /// <returns></returns>
//        public bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
//            string modulePath = Path.Combine(moduleDirectory, moduleName);

//            if (Directory.Exists(modulePath)) {
//                if (File.Exists(Path.Combine(modulePath, DependencyManifest.PathToDependencyManifestFile))) {
//                    manifest = DependencyManifest.LoadFromModule(modulePath);
//                    return true;
//                }
//            }

//            manifest = null;
//            return false;
//        }

//        /// <summary>
//        /// Tries to the get the path to the dependency manifest for a given module.
//        /// </summary>
//        /// <param name="moduleName">Name of the module.</param>
//        /// <param name="manifestPath">The manifest path.</param>
//        /// <returns></returns>
//        public bool TryGetDependencyManifestPath(string moduleName, out string manifestPath) {
//            string dependencyManifest = Path.Combine(moduleDirectory, moduleName, DependencyManifest.PathToDependencyManifestFile);

//            if (File.Exists(dependencyManifest)) {
//                manifestPath = dependencyManifest;
//                return true;
//            }

//            manifestPath = null;
//            return false;
//        }

//        /// <summary>
//        /// Determines whether the specified module is available to the current branch.
//        /// </summary>
//        /// <param name="moduleName">Name of the module.</param>
//        /// <returns>
//        ///   <c>true</c> if the specified module name is available; otherwise, <c>false</c>.
//        /// </returns>
//        public bool IsAvailable(string moduleName) {
//            return File.Exists(Path.Combine(moduleDirectory, moduleName, "Build", "TFSBuild.proj"));
//        }
//    }
        public void GetModulesInPendingChanges(string branchModulesDirectory) {

            //  var info = Workstation.Current.GetAllLocalWorkspaceInfo();

            // info[0].GetWorkspace((TfsTeamProjectCollection)teamFoundationFactory).GetPendingChanges()

        }
    }
}