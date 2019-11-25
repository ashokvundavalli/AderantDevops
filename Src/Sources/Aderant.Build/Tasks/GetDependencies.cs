using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Aderant.Build.Commands;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class GetDependencies : Task, ICancelableTask {
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        
        public string ModulesRootPath { get; set; }

        public string DropPath { get; set; }

        public string ProductManifest { get; set; }

        /// <summary>
        /// Gets or sets the name of the module consuming the dependencies.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public string ModuleName { get; set; }

        /// <summary>
        /// Gets or sets the directory to place the dependencies into.
        /// </summary>
        /// <value>The dependencies directory.</value>
        public string DependenciesDirectory { get; set; }

        /// <summary>
        /// Gets or sets the modules in the build set.
        /// </summary>
        /// <value>
        /// The modules in build.
        /// </value>
        public ITaskItem[] ModulesInBuild { get; set; }

        /// <summary>
        /// Gets or sets the path to the branch configuration file.
        /// </summary>
        /// <value>
        /// The branch configuration file.
        /// </value>
        public string BranchConfigFile { get; set; }

        /// <summary>
        /// Determines if Paket update should be run.
        /// </summary>
        /// <value>
        /// Whether or not to run the update command.
        /// </value>
        public bool Update { get; set; }

        public bool DisableSharedDependencyDirectory { get; set; }

        internal XDocument ConfigurationXml { get; set; }

        public string[] EnabledResolvers {
            get {
                return new string[0];
            }
            set {
                if (ConfigurationXml == null) {
                    if (value != null && value.Length > 0) {
                        var resolvers = new XElement("DependencyResolvers");
                        foreach (string name in value) {
                            resolvers.Add(new XElement(name));
                        }

                        var doc = new XDocument();
                        doc.Add(new XElement("BranchConfig", resolvers));

                        ConfigurationXml = doc;
                    }
                }
            }
        }

        public override bool Execute() {
            BuildTaskLogger logger = new BuildTaskLogger(this);            

            ExecuteInternal(logger);

            return !Log.HasLoggedErrors;
        }

        public void ExecuteInternal(Logging.ILogger logger) {
            ModulesRootPath = Path.GetFullPath(ModulesRootPath);

            ExpertManifest productManifest = null;
            if (!string.IsNullOrWhiteSpace(ProductManifest)) {
                ProductManifest = Path.GetFullPath(ProductManifest);

                if (File.Exists(ProductManifest)) {
                    productManifest = ExpertManifest.Load(ProductManifest);
                    productManifest.ModulesDirectory = ModulesRootPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(BranchConfigFile)) {
                ConfigurationXml = XDocument.Load(BranchConfigFile);
            }

            LogParameters(logger);

            var workflow = new ResolverWorkflow(logger) {
                ConfigurationXml = ConfigurationXml,
                ModulesRootPath = ModulesRootPath,
                DropPath = DropPath
            };

            if (DisableSharedDependencyDirectory) {
                workflow.Request.ReplicationExplicitlyDisabled = true;
            } else {
                if (string.IsNullOrWhiteSpace(DependenciesDirectory)) {
                    string dependenciesSubDirectory = ConfigurationXml.Descendants("DependenciesDirectory").FirstOrDefault()?.Value;

                    if (!string.IsNullOrWhiteSpace(dependenciesSubDirectory)) {
                        DependenciesDirectory = Path.Combine(ModulesRootPath, dependenciesSubDirectory);
                    } else {
                        workflow.Request.ReplicationExplicitlyDisabled = true;
                    }
                }
            }

            workflow.DependenciesDirectory = DependenciesDirectory;
            workflow.Request.ModuleFactory = productManifest;

            if (!string.IsNullOrEmpty(ModuleName)) {
                workflow.Request.AddModule(ModuleName);
            }

            if (ModulesInBuild != null) {
                foreach (ITaskItem module in ModulesInBuild) {
                    workflow.Request.AddModule(module.ItemSpec);
                }
            }

            if (Update) {
                workflow.Request.Update = Update;
            }

            workflow.Run(cancellationTokenSource.Token);
        }

        /// <summary>
        /// Attempts to cancel this instance.
        /// </summary>
        public void Cancel() {
            if (cancellationTokenSource != null) {
                // Signal the cancellation token that we want to abort the async task
                cancellationTokenSource.Cancel();
            }
        }
        
        private void LogParameters(Logging.ILogger logger) {
            logger.Info($"ModulesRootPath: {ModulesRootPath}");
            logger.Info($"DropPath: {DropPath}");
            logger.Info($"ProductManifest: {ProductManifest}");
            logger.Info($"Branch configuration file: {BranchConfigFile}");
        }
    }
}
