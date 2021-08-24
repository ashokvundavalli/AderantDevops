using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class UpdateAction : IDependencyAction {
        private readonly Dependencies dependencies;
        private readonly bool force;

        /// <param name="dependencies">The dependency model</param>
        /// <param name="force">Force the download and reinstallation of all packages</param>
        public UpdateAction(Dependencies dependencies, bool force) {
            this.dependencies = dependencies;
            this.force = force;
        }

        public void Run(PaketPackageManager paketPackageManager, CancellationToken cancellationToken = default(CancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();

            paketPackageManager.DoOperationWithCorruptPackageHandling(() => {
                Debug.Assert(dependencies != null);

                UpdateProcess.Update(dependenciesFileName: dependencies.DependenciesFile,
                    options: new UpdaterOptions(
                        common: new InstallerOptions(force: force,
                            semVerUpdateMode: SemVerUpdateMode.NoRestriction,
                            redirects: Requirements.BindingRedirectsSettings.Off /* Create binding redirects for the NuGet packages */,
                            alternativeProjectRoot: FSharpOption<string>.None,
                            cleanBindingRedirects: false,
                            createNewBindingFiles: false,
                            onlyReferenced: false /* Only install packages that are referenced in paket.references files */,
                            generateLoadScripts: false,
                            providedScriptTypes: FSharpList<string>.Empty,
                            providedFrameworks: FSharpList<string>.Empty,
                            touchAffectedRefs: false /* Touch projects referencing installed packages even if the project file does not change. */),
                        noInstall: false /* Installs into projects */));
            });
        }
    }
}