using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            string[] lines = file.Lines;
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];

                if (line.IndexOf("source " + BuildConstants.NugetServerUrl, StringComparison.OrdinalIgnoreCase) >= 0) {
                    break;
                }

                if (line.IndexOf("source " + Constants.DefaultNuGetStream, StringComparison.OrdinalIgnoreCase) >= 0) {
                    lines[i] = "source " + BuildConstants.NugetServerUrl;

                    fileSystem.MakeFileWritable(file.FileName);
                    file.Save();
                    break;
                }
            }

            foreach (var referencedModule in referencedModules.OrderBy(m => m.Name)) {
                if (referencedModule.ModuleType == ModuleType.ThirdParty) {
                    dependencies.Add(new FSharpOption<string>(group), referencedModule.Name, "", true, true, false, false, false, false, SemVerUpdateMode.NoRestriction, false);
                } else {
                    dependencies.Add(new FSharpOption<string>(group), referencedModule.Name, ">= 8.1 " + group, true, true, false, false, false, false, SemVerUpdateMode.NoRestriction, false);
                }
            }
        }

        private bool HasLockFile() {
            return fileSystem.FileExists(dependencies.GetDependenciesFile().FindLockfile().FullName);
        }

        public async Task Restore() {
            await Task.Run(() => {
                if (!HasLockFile()) {
                    dependencies.Update(false);
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

        internal class ObservableLogReceiver : FSharpFunc<Paket.Logging.Trace, Unit> {
            private readonly ILogger logger;

            public ObservableLogReceiver(ILogger logger) {
                this.logger = logger;
            }

            public override Unit Invoke(Paket.Logging.Trace trace) {
                if (trace.Level == TraceLevel.Verbose) {
                    logger.Debug(trace.Text);
                }

                if (trace.Level == TraceLevel.Info) {
                    logger.Info(trace.Text);
                }

                if (trace.Level == TraceLevel.Warning) {
                    logger.Warning(trace.Text);
                }

                if (trace.Level == TraceLevel.Error) {
                    logger.Error(trace.Text);
                }

                return null;
            }
        }

        public void Hack() {
            dependencies.Add(new FSharpOption<string>(group), "Aderant.Build.Analyzer", "", true, true, false, false, false, false, SemVerUpdateMode.NoRestriction, false);
            DependenciesFile file = dependencies.GetDependenciesFile();

            fileSystem.MakeFileWritable(file.FileName);
            file.Save();
        }
    }
}