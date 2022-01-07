using System.Threading;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class UpdateAction : IDependencyAction {
        private readonly Dependencies dependencies;
        private readonly bool force;

        /// <param name="dependencies">The dependency model</param>
        /// <param name="force">Force the download and reinstallation of all packages (slow)</param>
        public UpdateAction(Dependencies dependencies, bool force) {
            this.dependencies = dependencies;
            this.force = force;
        }

        public void Run(PaketPackageManager packageManager, CancellationToken cancellationToken = default(CancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();

            packageManager.RunTool(dependencies.RootPath, string.Format("update {0} {1}",  force ? "--force" : string.Empty, packageManager.EnableVerboseLogging ? "--verbose" : string.Empty));
        }
    }
}