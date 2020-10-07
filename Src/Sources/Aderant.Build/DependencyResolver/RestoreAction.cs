using System.Threading;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class RestoreAction : IDependencyAction {
        private readonly Dependencies dependencies;
        private bool force;

        public RestoreAction(Dependencies dependencies, bool force) {
            this.dependencies = dependencies;
            this.force = force;
        }

        public void Run(CancellationToken cancellationToken) {
            FSharpList<string> groups = dependencies.GetGroups();

            foreach (string group in groups) {
                cancellationToken.ThrowIfCancellationRequested();
                dependencies.Restore(force, new FSharpOption<string>(group), FSharpList<string>.Empty, false, false, false, FSharpOption<string>.None, FSharpOption<string>.None);
            }
        }
    }
}
