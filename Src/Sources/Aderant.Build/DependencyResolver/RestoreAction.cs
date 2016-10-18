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

        public void Run() {
            FSharpList<string> groups = dependencies.GetGroups();

            foreach (var group in groups) {
                dependencies.Restore(force, new FSharpOption<string>(group), FSharpList<string>.Empty, false, false);
            }
        }
    }
}