using Microsoft.FSharp.Collections;
using Paket;

namespace Aderant.Build {
    internal class UpdateAction : IDependencyAction {
        private readonly Dependencies dependencies;
        private readonly bool force;

        public UpdateAction(Dependencies dependencies, bool force) {
            this.dependencies = dependencies;
            this.force = force;
        }

        public void Run() {
            FSharpList<string> groups = dependencies.GetGroups();
            foreach (var group in groups) {
                dependencies.UpdateGroup(group, force, false, false, false, false, SemVerUpdateMode.NoRestriction, false);
            }
        }
    }
}