using Aderant.Build.DependencyResolver;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Paket;

namespace Aderant.Build {
    internal class RestoreAction : IDependencyAction {
        private readonly Dependencies dependencies;

        public RestoreAction(Dependencies dependencies) {
            this.dependencies = dependencies;
        }

        public void Run() {
            FSharpList<string> groups = dependencies.GetGroups();

            foreach (var group in groups) {
                dependencies.Restore(new FSharpOption<string>(group), FSharpList<string>.Empty);
            }
        }
    }
}