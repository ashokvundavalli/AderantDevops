using System;
using System.Threading;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class UpdateAction : IDependencyAction {
        private readonly Dependencies dependencies;
        private readonly bool force;

        public UpdateAction(Dependencies dependencies, bool force) {
            this.dependencies = dependencies;
            this.force = force;
        }

        public void Run(PaketPackageManager paketPackageManager, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            paketPackageManager.DoOperationWithCorruptPackageHandling(() => {
                UpdateProcess.Update(dependenciesFileName: dependencies.DependenciesFile,
                    options: new UpdaterOptions(
                        common: new InstallerOptions(force: force,
                            semVerUpdateMode: SemVerUpdateMode.NoRestriction,
                            redirects: Requirements.BindingRedirectsSettings.Off,
                            alternativeProjectRoot: FSharpOption<string>.None,
                            cleanBindingRedirects: false,
                            createNewBindingFiles: false,
                            onlyReferenced: false,
                            generateLoadScripts: false,
                            providedScriptTypes: FSharpList<string>.Empty,
                            providedFrameworks: FSharpList<string>.Empty,
                            touchAffectedRefs: false),
                        noInstall: false));
            });
        }
    }
}