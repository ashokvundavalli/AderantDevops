using System.IO;
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

        public string ConfigurationXml { get; set; }

        public string[] EnabledResolvers {
            get {
                return new string[0];
            }
            set {
                if (string.IsNullOrEmpty(ConfigurationXml)) {
                    if (value != null && value.Length > 0) {
                        var resolvers = new XElement("DependencyResolvers");
                        foreach (string name in value) {
                            resolvers.Add(new XElement(name));
                        }

                        var doc = new XDocument();
                        doc.Add(new XElement("BranchConfig", resolvers));

                        ConfigurationXml = doc.ToString();
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
            if (ProductManifest != null) {
                ProductManifest = Path.GetFullPath(ProductManifest);
                productManifest = ExpertManifest.Load(ProductManifest);
                productManifest.ModulesDirectory = ModulesRootPath;
            }

            LogParameters(logger);

            var workflow = new ResolverWorkflow(logger);
            workflow.ConfigurationXml = ConfigurationXml;
            workflow.ModulesRootPath = ModulesRootPath;
            workflow.DropPath = DropPath;
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
        }
    }
}
