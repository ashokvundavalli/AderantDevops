using System.Threading;

namespace Aderant.Build.DependencyResolver {
    internal interface IDependencyAction {
        void Run(CancellationToken cancellationToken);
    }
}
