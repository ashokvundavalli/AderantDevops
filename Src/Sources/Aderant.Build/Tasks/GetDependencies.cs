using System;
using System.Linq;
using System.Threading;
using Aderant.Build.DependencyResolver;
using Aderant.Build.DependencyResolver.Resolvers;
using Aderant.Build.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class GetDependencies : Task, ICancelableTask {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool? enableReplication;

        public bool EnableVerboseLogging { get; set; }

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
        public ITaskItem[] ModulesInBuild { get; set; } = Array.Empty<ITaskItem>();

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

        public bool EnableReplication {
            get { return enableReplication.GetValueOrDefault(false); }
            set { enableReplication = value; }
        }

        /// <remarks>
        /// NupkgResolver is enabled by default if no resolvers are specified.
        /// </remarks>
        public string[] EnabledResolvers { get; set; } = {nameof(NupkgResolver)};

        public override bool Execute() {
            BuildTaskLogger logger = new BuildTaskLogger(this);

            ExecuteInternal(logger);

            return !Log.HasLoggedErrors;
        }

        private void ExecuteInternal(Logging.ILogger logger) {
            LogParameters(logger);

            var workflow = new ResolverWorkflow(logger, new PhysicalFileSystem()) {
                DropPath = DropPath
            };

            workflow
                .WithRootPath(ModulesRootPath)
                .UseReplication(EnableReplication)
                .WithConfigurationFile(BranchConfigFile)
                .WithResolvers(EnabledResolvers)
                .WithProductManifest(ProductManifest)
                .UseDependenciesDirectory(DependenciesDirectory)
                .WithDirectoriesInBuild(ModulesInBuild.Select(s => s.ItemSpec).Where(x => !x.StartsWith("_")).Union(new[] {ModuleName}, StringComparer.OrdinalIgnoreCase));

            workflow.Run(Update, EnableVerboseLogging, cancellationTokenSource.Token);
        }

        /// <summary>
        /// Attempts to cancel this instance.
        /// </summary>
        public void Cancel() {
            if (cancellationTokenSource != null) {
                // Signal the cancellation token that we want to abort the async task.
                cancellationTokenSource.Cancel();
            }
        }

        private void LogParameters(Logging.ILogger logger) {
            logger.Info($"ModulesRootPath: {ModulesRootPath}");
            logger.Info($"DropPath: {DropPath}");
            logger.Info($"ProductManifest: {ProductManifest}");
            logger.Info($"Branch configuration file: {BranchConfigFile}");
            logger.Info($"EnableReplication: {EnableReplication}");
        }
    }
}
