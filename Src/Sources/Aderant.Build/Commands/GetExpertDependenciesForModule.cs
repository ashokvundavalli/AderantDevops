using System;
using System.Management.Automation;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.Get, "ExpertDependenciesForModule")]
    public class GetExpertDependenciesForModule : BuildCmdlet {
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        [Parameter(Mandatory = false, Position = 0)]
        public string ModuleName { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public string ModulesRootPath { get; set; }

        /// <summary>
        /// Gets or sets the directory to place the dependencies into.
        /// </summary>
        /// <value>The dependencies directory.</value>
        [Parameter(Mandatory = false, Position = 2)]
        public string DependenciesDirectory { get; set; }

        [Parameter(Mandatory = false, Position = 3)]
        public string DropPath { get; set; }

        [Parameter(Mandatory = false, Position = 4)]
        public SwitchParameter Update { get; set; }

        [Parameter(Mandatory = false, Position = 5)]
        public SwitchParameter ShowOutdated { get; set; }

        [Parameter(Mandatory = false, Position = 6)]
        public SwitchParameter Force { get; set; }

        [Parameter(Mandatory = false, Position = 7, HelpMessage = "Specifies the path the product manifest.")]
        public string ProductManifestPath { get; set; }

        [Parameter(Mandatory = false, Position = 8, DontShow = true)]
        public string ManifestFile { get; set; }

        protected override void Process() {
            Logger.Info($"Module set to {ModuleName}");

            if (string.IsNullOrEmpty(ModuleName)) {
                ModuleName = ParameterHelper.GetCurrentModuleName(null, this.SessionState);
            }

            try {
                if (string.IsNullOrEmpty(ProductManifestPath)) {
                    ProductManifestPath = ParameterHelper.GetExpertManifestPath(ProductManifestPath, this.SessionState);
                }

                if (!string.IsNullOrEmpty(ProductManifestPath)) {
                    var productManifest = ExpertManifest.Load(ProductManifestPath);
                    productManifest.ModulesDirectory = ModulesRootPath;
                }
            } catch (ArgumentException) {
                //git modules don't need ProductManifestPath
            }

            if (string.IsNullOrEmpty(DropPath)) {
                DropPath = ParameterHelper.GetDropPath(null, SessionState);
            }

            var workflow = new ResolverWorkflow(Logger);

            workflow.ModulesRootPath = ModulesRootPath;
            workflow.ManifestFile = ManifestFile;
            workflow.DropPath = DropPath;
            workflow.Force = Force.ToBool();
            workflow.Update = Update.ToBool();

            if (!string.IsNullOrEmpty(ModulesRootPath)) {
                workflow.Request.AddModule(ModulesRootPath);
            }

            workflow.Run(cancellationTokenSource.Token);
        }

        protected override void StopProcessing() {
            base.StopProcessing();
            cancellationTokenSource.Cancel();
        }
    }
}