using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Aderant.Build.DependencyResolver.Models;
using Aderant.Build.Logging;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class PaketPackageManager : IDisposable {
        private readonly FSharpHandler<Paket.Logging.Trace> logMessageDelegate;
        private readonly string root;
        private readonly ILogger logger;
        public IFileSystem FileSystem { get; }
        public static string DependenciesFile { get; } = "paket.dependencies";

        private Dependencies dependencies;

        public PaketPackageManager(string root, IFileSystem2 fileSystem, ILogger logger) {
            this.root = root;
            this.logger = logger;
            this.FileSystem = fileSystem;
            dependencies = Initialize(root);

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

        private Dependencies Initialize(string root) {
            try {
                var file = FileSystem.GetFiles(root, DependenciesFile, true).FirstOrDefault();
                if (file != null) {
                    dependencies = Dependencies.Locate(root);
                }
            } catch (Exception) {
            } finally {
                if (dependencies == null) {
                    // If the dependencies file doesn't exist Paket will scan up until it finds one, which causes massive problems
                    // as it will no doubt locate something it shouldn't use (eg one from another product)
                    Dependencies.Init(root);
                    dependencies = Dependencies.Locate(root);
                }
            }

            return dependencies;
        }

        public void Add(IEnumerable<IDependencyRequirement> requirements) {
            DependenciesFile file = dependencies.GetDependenciesFile();
            FileSystem.MakeFileWritable(file.FileName);
            AddModules(requirements, ref file);

            PaketDependenciesStructure structure = new PaketDependenciesStructure(file.Lines);
            file = new DependenciesFile(file.FileName, MapModule.Empty<Domain.GroupName, DependenciesGroup>(), structure.Content);

            Lines = file.Lines;

            file.Save();
        }

        public string[] Lines {
            get;
            private set;
        }

        // Paket is unable to write version ranges to file.
        private string RemoveVersionRange(string name, string version) {
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(name) || !name.StartsWith("Aderant.", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("EE.", StringComparison.OrdinalIgnoreCase)) {
                return version;
            }

            string[] parts = version.Split(' ').ToArray();
            // ">= 11.0 < 12.0 build" will become "< 12.0 build"
            if (parts.Length >= 4 && parts[0] == ">=" && parts[2] == "<") {
                string newVersion = string.Join(" ", parts.Skip(2));
                logger.Info($"Version Adjusted {name}: '{version}' to: '{newVersion}'");

                return newVersion;
            }

            return version;
        }

        private void AddModules(IEnumerable<IDependencyRequirement> requirements, ref DependenciesFile file) {
            foreach (var requirement in requirements.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)) {
                bool hasCustomVersion = false;
                string version = string.Empty;

                if (requirement.VersionRequirement != null && !string.IsNullOrWhiteSpace(requirement.VersionRequirement.ConstraintExpression)) {
                    hasCustomVersion = true;
                    version = requirement.VersionRequirement.ConstraintExpression;
                }

                var packageName = Domain.PackageName(requirement.Name);
                var groupName = Domain.GroupName(requirement.Group);

                if (requirement.ReplaceVersionConstraint && hasCustomVersion) {
                    try {
                        file = file.Remove(Domain.GroupName(requirement.Group), packageName);
                    } catch {
                    }
                }

                if (!file.HasPackage(groupName, packageName)) {
                    version = RemoveVersionRange(requirement.Name, version);

                    try {
                        file = file.Add(Domain.GroupName(requirement.Group), Domain.PackageName(requirement.Name), version, FSharpOption<Requirements.InstallSettings>.None);
                    } catch (Exception ex) {
                        if (requirement.VersionRequirement != null && requirement.VersionRequirement.OriginatingFile != null) {
                            string message = ex.Message;
                            if (!message.EndsWith(".")) {
                                message += ".";
                            }

                            throw new InvalidOperationException(message + " The source file which caused the error was " + requirement.VersionRequirement.OriginatingFile);
                        }
                        throw;
                    }
                }
            }
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
            dependencies.ShowOutdated(true, false, FSharpOption<string>.Some(Constants.MainDependencyGroup));
        }

        public void Dispose() {
            Paket.Logging.@event.Publish.RemoveHandler(logMessageDelegate);
        }

        public IDictionary<string, VersionRequirement> GetDependencies() {
            return GetDependencies(Constants.MainDependencyGroup);
        }

        public IDictionary<string, VersionRequirement> GetDependencies(string groupName) {
            Dependencies dependenciesFile = Dependencies.Locate(root);
            var file = dependenciesFile.GetDependenciesFile();

            try {
                FSharpMap<Domain.PackageName, Paket.VersionRequirement> requirements = file.GetDependenciesInGroup(Domain.GroupName(groupName));
                return requirements.ToDictionary(pair => pair.Key.ToString(), pair => NewRequirement(pair, file.FileName));
            } catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }

        private static readonly Regex invalidVersionPattern = new Regex("^0[.]0[.]0-\\w*");
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

            if (invalidVersionPattern.IsMatch(pair.Value.FormatInNuGetSyntax())) {
                logger.Error($"Invalid version expression for requirement {pair.Key.Item1} in file {filePath}. Does this requirement have any operators ('>', '<', '=') specified? Please fix the version.");
                // Dirty fix, if we have a invalid requirement pattern that has no operators we need to convert this to a
                // valid paket pattern otherwise a parse exception will occur. However the paket API is dreadful and does not implement ToString() for "min version"
                // so I cheat and check if the NuGet format of the  requirement as this seems to be a reliable marker that we need to fudge the input data.
                throw new Exception("Version format is incorrect.");
            }

            return new VersionRequirement {
                OriginatingFile = filePath,
                ConstraintExpression = $"{expression} {string.Join(" ", prereleases)}",
            };
        }

        public IEnumerable<string> FindGroups() {
            Dependencies dependenciesFile = Dependencies.Locate(root);
            var file = dependenciesFile.GetDependenciesFile();
            return file.Groups.Select(s => s.Key.Item1);
        }
    }
}
