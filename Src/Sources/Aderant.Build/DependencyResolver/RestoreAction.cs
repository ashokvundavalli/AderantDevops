using System;
using System.IO;
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

        public void Run(PaketPackageManager paketPackageManager, CancellationToken cancellationToken) {
            FSharpList<string> groups = dependencies.GetGroups();

            foreach (string group in groups) {
                cancellationToken.ThrowIfCancellationRequested();

                paketPackageManager.DoOperationWithCorruptPackageHandling(() => {
                    dependencies.Restore(force: force,
                        @group: new FSharpOption<string>(group),
                        files: FSharpList<string>.Empty,
                        touchAffectedRefs: false,
                        ignoreChecks: false,
                        failOnChecks: false,
                        targetFramework: FSharpOption<string>.None,
                        outputPath: FSharpOption<string>.None);
                });
            }
        }


    }
}
