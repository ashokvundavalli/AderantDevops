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
    internal class DependencyManager : IDisposable {
        static string group = Constants.MainDependencyGroup.ToString();

        public IFileSystem2 FileSystem { get; }
        public static string DependenciesFile { get; } = "paket.dependencies";

        private IDisposable traceEventsSubscription;
        private Dependencies dependencies;

        public DependencyManager(IFileSystem2 fileSystem, ILogger logger) {
            this.FileSystem = fileSystem;
            dependencies = Initialize();

            traceEventsSubscription = CommonExtensions.SubscribeToObservable(Paket.Logging.@event.Publish, new ObservableLogReceiver(logger));
        }

        private Dependencies Initialize() {
            // TODO: Bug in Paket Dependencies.Locate - it always prefers to search up

            try {
                //dependencies = Dependencies.Locate(FileSystem.Root);
                var dependencyFile = FileSystem.GetFiles(FileSystem.Root, DependenciesFile, true).FirstOrDefault();
                if (dependencyFile != null) {
                    dependencies = new Dependencies(FileSystem.GetFullPath(dependencyFile));
                }
            } catch (Exception) {
                
            } finally {
                if (dependencies == null) {
                    Dependencies.Init(FileSystem.Root);
                    dependencies = Dependencies.Locate(FileSystem.Root);
                }
            }
            return dependencies;
        }

        public void Add(IEnumerable<ExpertModule> referencedModules) {
            var file = dependencies.GetDependenciesFile();

            FileSystem.MakeFileWritable(file.FileName);

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
                    // As soon as we have a version constraint for a package this will fail as we are passing "" for the package version.

                    string version = string.Empty;
                    if (referencedModule.VersionRequirement != null) {
                        version = referencedModule.VersionRequirement.ConstraintExpression;
                    }

                    file = file.Add(Constants.MainDependencyGroup, Domain.PackageName(referencedModule.Name), version, FSharpOption<Requirements.InstallSettings>.None);
                }
            }

            file.Save();
        }

        private bool HasLockFile() {
            return FileSystem.FileExists(dependencies.GetDependenciesFile().FindLockfile().FullName);
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
            await Task.Run(() => { dependencies.ShowOutdated(true, false); });
        }

        public void Dispose() {
            traceEventsSubscription.Dispose();
        }

        public void Hack() {
            dependencies.Add(new FSharpOption<string>(group), "Aderant.Build.Analyzer", "", true, true, false, false, false, false, SemVerUpdateMode.NoRestriction, false);
            DependenciesFile file = dependencies.GetDependenciesFile();

            FileSystem.MakeFileWritable(file.FileName);
            file.Save();
        }

        public VersionRequirement GetVersionsFor(string name) {
            Dependencies dependenciesFile = Dependencies.Locate(FileSystem.Root);
            var file = dependenciesFile.GetDependenciesFile();

            try {
                var packageRequirement = file.GetPackage(Constants.MainDependencyGroup, Domain.PackageName(name));

                if (packageRequirement != null) {
                    string syntax = packageRequirement.VersionRequirement.ToString();

                    if (!string.IsNullOrEmpty(syntax)) {
                        return new VersionRequirement {
                            ConstraintExpression = syntax
                        };
                    }
                }
            } catch (KeyNotFoundException) {
                // No entry for package
                return null;
            }

            return null;
        }
    }

    internal class VersionRequirement {
        public string ConstraintExpression { get; set; }
    }
}