using System.Threading;

namespace Aderant.Build.DependencyResolver {
    internal interface IDependencyAction {
        void Run(PaketPackageManager manager, CancellationToken cancellationToken = default(CancellationToken));
    }
}
