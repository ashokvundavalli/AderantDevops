using System.Threading;

namespace Aderant.Build.DependencyResolver {
    internal interface IDependencyAction {
        void Run(PaketPackageManager packageManager, CancellationToken cancellationToken = default(CancellationToken));
    }
}
