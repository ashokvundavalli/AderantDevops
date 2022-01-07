using System.Threading;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class RestoreAction : IDependencyAction {

        private readonly Dependencies dependencies;
        private bool force;

        public RestoreAction(Dependencies dependencies, bool force) {
            this.dependencies = dependencies;
            this.force = force;
        }

        public void Run(PaketPackageManager packageManager, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            packageManager.RunTool(dependencies.RootPath, string.Format("restore {0} {1}", force ? "--force" : string.Empty, packageManager.EnableVerboseLogging ? "--verbose" : string.Empty));
        }
    }
}