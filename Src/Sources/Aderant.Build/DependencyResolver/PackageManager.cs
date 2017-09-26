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
                    try {
                        file = file.Add(Domain.GroupName(BuildConstants.MainDependencyGroup), Domain.PackageName(referencedModule.Name), version, FSharpOption<Requirements.InstallSettings>.None);
                    } catch (Exception ex) {
                        if (referencedModule.VersionRequirement != null && referencedModule.VersionRequirement.OriginatingFile != null) {
                            string message = ex.Message;
                            if (!message.EndsWith(".")) {
                                message += ".";
                            }

                            throw new InvalidOperationException(message + " The source file which caused the error was " + referencedModule.VersionRequirement.OriginatingFile);
                        }
                        throw;
                    }
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

            return requirements.ToDictionary(pair => pair.Key.ToString(), pair => NewRequirement(pair, file.FileName));
        }

        private VersionRequirement NewRequirement(KeyValuePair<Domain.PackageName, Paket.VersionRequirement> pair, string filePath) {
            List<string> prereleases = new List<string>();

            if (pair.Value.PreReleases.IsConcrete) {
                PreReleaseStatus.Concrete concrete = pair.Value.Item2 as Paket.PreReleaseStatus.Concrete;
                if (concrete != null) {
                    prereleases.Add(concrete.Item.HeadOrDefault);

                    FSharpList<string> item = concrete.Item.TailOrNull;
                    while (item != null) {
                        if (!item.IsEmpty) {
                            if (item.Head != null) {
                                prereleases.Add(item.HeadOrDefault);
                            }
                        }

                        item = item.TailOrNull;
                    }
                }
            }

            string expression = pair.Value.ToString();

            if (pair.Value.FormatInNuGetSyntax() == "0.0.0-prerelease") {
                logger.Warning($"Invalid version expression for requirement {pair.Key.Item1} in file {filePath}. Does this requirement have any operators ('>', '<', '=') specified? This expression will be converted into >= 0");
                // Dirty fix, if we have a invalid requirement pattern that has no operators we need to convert this to a 
                // valid paket pattern otherwise a parse exception will occur. However the paket API is dreadful and does not implement ToString() for "min version"
                // so I cheat and check if the NuGet format of the  requirement as this seems to be a reliable marker that we need to fudge the input data.
                expression = ">= 0";
            }

            return new VersionRequirement {
                OriginatingFile = filePath,
                ConstraintExpression = $"{expression} {string.Join(" ", prereleases)}",
            };
        }
    }
}
