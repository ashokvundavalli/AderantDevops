using System;
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

        public void Run() {
            FSharpList<string> groups = dependencies.GetGroups();

            using (Mutex myMutex = new Mutex(false, "7C1226B2-0D90-4DAA-9D87-18EF02BD8021")) {
                try {
                    myMutex.WaitOne();

                    foreach (var group in groups) {
                        dependencies.Restore(force, new FSharpOption<string>(group), FSharpList<string>.Empty, false, false);
                    }
                } finally {
                    myMutex.ReleaseMutex();
                }
            }
        }
    }
}
