using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Paket;

namespace Aderant.Build {
    public class DependencyRetriever : IDisposable {
        static string group = Constants.MainDependencyGroup.ToString();

        private readonly IFileSystem2 fileSystem;
        private IDisposable traceEventsSubscription;
        private Dependencies dependencies;

        public DependencyRetriever(IFileSystem2 fileSystem, ILogger logger) {
            this.fileSystem = fileSystem;
            dependencies = Initialize();

            traceEventsSubscription = CommonExtensions.SubscribeToObservable(Paket.Logging.@event.Publish, new ObservableLogReceiver(logger));
        }

        private Dependencies Initialize() {
            try {
                dependencies = Dependencies.Locate(fileSystem.Root);
            } catch (Exception) {
                Dependencies.Init(fileSystem.Root);
                dependencies = Dependencies.Locate(fileSystem.Root);
            }
            return dependencies;
        }

        public void Add(IEnumerable<ExpertModule> referencedModules) {
            var file = dependencies.GetDependenciesFile();

            fileSystem.MakeFileWritable(file.FileName);

            string[] lines = file.Lines;
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];

                if (line.IndexOf("source " + BuildConstants.NugetServerUrl, StringComparison.OrdinalIgnoreCase) >= 0) {
                    break;
                }

                if (line.IndexOf("source " + Constants.DefaultNuGetStream, StringComparison.OrdinalIgnoreCase) >= 0) {
                    lines[i] = "source " + BuildConstants.NugetServerUrl;
                    file.Save();
                    break;
                }
            }

            AddModules(referencedModules, file);
        }

        private void AddModules(IEnumerable<ExpertModule> referencedModules, DependenciesFile file) {
            foreach (var referencedModule in referencedModules.OrderBy(m => m.Name)) {
                if (referencedModule.ModuleType == ModuleType.ThirdParty || referencedModule.GetAction == GetAction.NuGet) {
                    // This is not the correct API. One should use dependencies.Add(...) but this is much faster as it doesn't call the server
                    // As soon as we have a version constraint for a package this will fail as we are passing "" for the package version..
                    file = file.Add(Constants.MainDependencyGroup, Domain.PackageName(referencedModule.Name), string.Empty, FSharpOption<Requirements.InstallSettings>.None);
                }
            }
            
            file.Save();
        }

        private bool HasLockFile() {
            return fileSystem.FileExists(dependencies.GetDependenciesFile().FindLockfile().FullName);
        }

        public async Task Restore() {
            await Task.Run(() => {
                if (!HasLockFile()) {
                    dependencies.Update(false, false, false, false, false, SemVerUpdateMode.NoRestriction, false);
                }

                dependencies.Restore();
            });
        }

        public async Task Update(bool force) {
            await Task.Run(() => { dependencies.Update(force, false, false, false, false, SemVerUpdateMode.NoRestriction, false); });
        }

        public async Task ShowOutdated() {
            await Task.Run(() => {
                dependencies.ShowOutdated(true, false);
            });
        }

        public void Dispose() {
            traceEventsSubscription.Dispose();
        }

        public void Hack() {
            dependencies.Add(new FSharpOption<string>(group), "Aderant.Build.Analyzer", "", true, true, false, false, false, false, SemVerUpdateMode.NoRestriction, false);
            DependenciesFile file = dependencies.GetDependenciesFile();

            fileSystem.MakeFileWritable(file.FileName);
            file.Save();
        }
    }
}