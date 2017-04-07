using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build.Logging;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class PackageManager : IDisposable {
        private FSharpHandler<Paket.Logging.Trace> logMessageDelegate;
        private readonly ILogger logger;
        public IFileSystem2 FileSystem { get; }
        public static string DependenciesFile { get; } = "paket.dependencies";

        private Dependencies dependencies;

        public PackageManager(IFileSystem2 fileSystem, ILogger logger) {
            this.logger = logger;
            this.FileSystem = fileSystem;
            dependencies = Initialize();

            this.logMessageDelegate = OnTraceEvent;

            Paket.Logging.@event.Publish.AddHandler(logMessageDelegate);
        }

        private void OnTraceEvent(object sender, Paket.Logging.Trace args) {
            if (args.Level == TraceLevel.Verbose) {
                logger.Debug(args.Text);
            }

            if (args.Level == TraceLevel.Info) {
                logger.Info(args.Text);
            }

            if (args.Level == TraceLevel.Warning) {
                logger.Warning(args.Text);
            }

            if (args.Level == TraceLevel.Error) {
                logger.Error(args.Text);
            }
        }

        private Dependencies Initialize() {
            try {
                var file = FileSystem.GetFiles(FileSystem.Root, DependenciesFile, true).FirstOrDefault();
                if (file != null) {
                    dependencies = Dependencies.Locate(FileSystem.Root);
                }
            } catch (Exception) {
            } finally {
                if (dependencies == null) {
                    // If the dependencies file doesn't exist Paket will scan up until it finds one, which causes massive problems 
                    // as it will no doubt locate something it shouldn't use (eg one from another product)
                    Dependencies.Init(FileSystem.Root);
                    dependencies = Dependencies.Locate(FileSystem.Root);
                }
            }
            return dependencies;
        }

        public void Add(IPackageContext context, IEnumerable<IDependencyRequirement> requirements) {
            var file = dependencies.GetDependenciesFile();

            FileSystem.MakeFileWritable(file.FileName);

            string[] lines = file.Lines;
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];

                if (line.IndexOf("source " + BuildConstants.PackageServerUrl, StringComparison.OrdinalIgnoreCase) >= 0) {
                    break;
                }

                if (line.IndexOf("source " + BuildConstants.DefaultNuGetServer, StringComparison.OrdinalIgnoreCase) >= 0) {
                    lines[i] = "source " + BuildConstants.PackageServerUrl + "\nsource " + BuildConstants.DatabasePackageUri;
                    file.Save();
                    break;
                }
            }

            AddModules(context, requirements, file);
        }

        private void AddModules(IPackageContext context, IEnumerable<IDependencyRequirement> requirements, DependenciesFile file) {
            foreach (var referencedModule in requirements.OrderBy(m => m.Name)) {
                string version = string.Empty;
                if (referencedModule.VersionRequirement != null) {
                    version = referencedModule.VersionRequirement.ConstraintExpression ?? ">= 0 build ci rc unstable";
                }

                var name = Domain.PackageName(referencedModule.Name);
                
                if (referencedModule.ReplaceVersionConstraint) {
                    try {
                        file = file.Remove(Domain.GroupName(BuildConstants.MainDependencyGroup), name);
                    } catch {
                        
                    }
                }

                if (string.IsNullOrEmpty(file.CheckIfPackageExistsInAnyGroup(name))) {
                    file = file.Add(Domain.GroupName(BuildConstants.MainDependencyGroup), Domain.PackageName(referencedModule.Name), version, FSharpOption<Requirements.InstallSettings>.None);
                }
            }

            file.Save();
        }

        private bool HasLockFile() {
            return FileSystem.FileExists(dependencies.GetDependenciesFile().FindLockfile().FullName);
        }

        public void Restore(bool force = false) {
            if (!HasLockFile()) {
                new UpdateAction(dependencies, force).Run();
            }
            new RestoreAction(dependencies, force).Run();
        }

        public void Update(bool force) {
            new UpdateAction(dependencies, force).Run();
        }

        public void ShowOutdated() {
            // TODO: Break UI binding - return a list
            dependencies.ShowOutdated(true, false, FSharpOption<string>.Some(BuildConstants.MainDependencyGroup));
        }

        public void Dispose() {
            Paket.Logging.@event.Publish.RemoveHandler(logMessageDelegate);
        }

        public IDictionary<string, VersionRequirement> GetDependencies() {
            Dependencies dependenciesFile = Dependencies.Locate(FileSystem.Root);
            var file = dependenciesFile.GetDependenciesFile();

            FSharpMap<Domain.PackageName, Paket.VersionRequirement> requirements = file.GetDependenciesInGroup(Domain.GroupName(BuildConstants.MainDependencyGroup));

            return requirements.ToDictionary(pair => pair.Key.ToString(), NewRequirement);
        }

        private VersionRequirement NewRequirement(KeyValuePair<Domain.PackageName, Paket.VersionRequirement> pair) {
            string tag = string.Empty;
            if (pair.Value.PreReleases.IsConcrete) {
                PreReleaseStatus.Concrete concrete = pair.Value.Item2 as Paket.PreReleaseStatus.Concrete;
                if (concrete != null) {
                    tag = concrete.Item.Head;
                }
            }

            return new VersionRequirement {
                ConstraintExpression = pair.Value.ToString() + " " + tag
            };
        }
    }
}