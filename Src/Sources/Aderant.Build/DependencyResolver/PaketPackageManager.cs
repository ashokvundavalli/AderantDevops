using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Aderant.Build.DependencyResolver.Models;
using Aderant.Build.Logging;
using Microsoft.FSharp.Collections;
using Paket;

namespace Aderant.Build.DependencyResolver {
    internal class PaketPackageManager : IDisposable {

        private static readonly Regex invalidVersionPattern = new Regex("^0[.]0[.]0-\\w*");

        private static readonly InstallOptions defaultInstallOptions = InstallOptions.Default;

        private static List<WeakReference<ILogger>> registeredLoggers;
        private static object syncLock = new object();
        private readonly ILogger logger;

        private readonly string root;
        private readonly IWellKnownSources wellKnownSources;
        private bool createdNew;
        private bool initialized;

        private Dependencies dependencies;
        private DependenciesFile dependenciesFile;


        static PaketPackageManager() {
            PaketHttpMessageHandlerFactory.Configure();
        }

        public PaketPackageManager(string root, IFileSystem2 fileSystem, IWellKnownSources wellKnownSources, ILogger logger, bool enableVerboseLogging = false) {
            this.root = root;
            this.wellKnownSources = wellKnownSources;
            this.logger = logger;
            this.FileSystem = fileSystem;

            Paket.Logging.verbose = enableVerboseLogging;

            bool attachLogger = true;

            // Prevent duplicate subscriptions from the same logger to the global paket event.
            lock (syncLock) {
                if (registeredLoggers == null) {
                    registeredLoggers = new List<WeakReference<ILogger>>();
                    Paket.Logging.@event.Publish.AddHandler(OnTraceEvent);
                }

                var loggers = GetLoggerReferences();

                foreach (var weakReference in loggers) {
                    ILogger reference;

                    if (weakReference.TryGetTarget(out reference)) {
                        if (ReferenceEquals(reference, logger)) {
                            continue;
                        }
                    } else {
                        registeredLoggers.Remove(weakReference);
                    }
                }

                if (attachLogger) {
                    registeredLoggers.Add(new WeakReference<ILogger>(logger));
                }
            }
        }

        public IFileSystem FileSystem { get; }
        public static string DependenciesFile { get; } = "paket.dependencies";

        public string[] Lines { get; private set; }


        public void Dispose() {
            void RemoveReference(WeakReference<ILogger> loggerReference) {
                lock (syncLock) {
                    registeredLoggers.Remove(loggerReference);
                }
            }

            var references = GetLoggerReferences();

            foreach (var loggerReference in references) {
                if (loggerReference.TryGetTarget(out var target)) {
                    if (ReferenceEquals(target, logger)) {
                        RemoveReference(loggerReference);
                    }
                } else {
                    RemoveReference(loggerReference);
                }
            }
        }

        internal static List<WeakReference<ILogger>> GetLoggerReferences() {
            List<WeakReference<ILogger>> references;
            lock (syncLock) {
                references = registeredLoggers.ToList();
            }

            return references;
        }

        private static void OnTraceEvent(object sender, Paket.Logging.Trace args) {
            var references = GetLoggerReferences();

            foreach (var loggerReference in references) {
                loggerReference.TryGetTarget(out var target);

                if (target != null) {
                    Log(target, args);
                }
            }
        }

        private static void Log(ILogger logger, Paket.Logging.Trace args) {
            if (args.Level == TraceLevel.Verbose) {
                logger.Info(args.Text, null);
                return;
            }

            if (args.Level == TraceLevel.Info) {
                logger.Info(args.Text, null);
                return;
            }

            if (args.Level == TraceLevel.Warning) {
                logger.Warning(args.Text, null);
                return;
            }

            if (args.Level == TraceLevel.Error) {
                if (args.Text.StartsWith("Could not detect any platforms from 'any'")) {
                    logger.Warning(args.Text, null);
                } else {
                    logger.Error(args.Text, null);
                }
            }
        }

        private void Initialize() {
            if (initialized) {
                return;
            }

            if (string.IsNullOrWhiteSpace(root)) {
                dependencies = null;
                initialized = true;
                return;
            }

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
                    Dependencies.Init(root, FSharpList<PackageSources.PackageSource>.Empty, FSharpList<string>.Empty, false);
                    createdNew = true;
                    dependencies = Dependencies.Locate(root);
                }
            }

            initialized = true;
        }

        public void Add(IEnumerable<IDependencyRequirement> requirements, ResolverRequest resolverRequest = null) {
            Initialize();

            dependenciesFile = dependencies.GetDependenciesFile();

            bool validatePackageConstraints = resolverRequest != null && resolverRequest.ValidatePackageConstraints;

            AddModules(requirements, validatePackageConstraints, ref dependenciesFile);

            FileSystem.MakeFileWritable(dependenciesFile.FileName);

            // Indicates if the system is resolving for a single directory or multiple directories.
            bool isUsingMultipleInputFiles = resolverRequest != null && resolverRequest.Modules.Count() > 1;

            FSharpMap<Domain.GroupName, DependenciesGroup> groups = dependenciesFile.Groups;

            List<DependenciesGroup> groupList = new List<DependenciesGroup>();

            foreach (var groupEntry in groups) {
                DependenciesGroup group = dependenciesFile.GetGroup(groupEntry.Key);

                bool isMainGroup = string.Equals(group.Name.Name, Constants.MainDependencyGroup, StringComparison.OrdinalIgnoreCase);

                FSharpList<PackageSources.PackageSource> sources = CreateSources(groupEntry, group, isMainGroup, isUsingMultipleInputFiles);
                InstallOptions options = CreateInstallOptions(requirements, groupEntry, group, isMainGroup);

                DependenciesGroup newGroup = new DependenciesGroup(
                    group.Name,
                    sources,
                    group.Caches,
                    options,
                    group.Packages,
                    group.ExternalLocks,
                    group.RemoteFiles);

                if (isMainGroup) {
                    groupList.Insert(0, newGroup);
                } else {
                    groupList.Add(newGroup);
                }
            }

            DependenciesFileWriter fileWriter = new DependenciesFileWriter();
            var result = fileWriter.Write(groupList, resolverRequest?.GetFrameworkRestrictions());

            FSharpMap<Domain.GroupName, DependenciesGroup> map = MapModule.Empty<Domain.GroupName, DependenciesGroup>();
            foreach (DependenciesGroup group in groupList) {
                map = map.Add(group.Name, group);
            }

            dependenciesFile = new DependenciesFile(
                dependenciesFile.FileName,
                map,
                result.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries));

            dependenciesFile.Save();

            Lines = dependenciesFile.Lines;
        }

        private FSharpList<PackageSources.PackageSource> CreateSources(KeyValuePair<Domain.GroupName, DependenciesGroup> groupEntry, DependenciesGroup group, bool isMainGroup, bool isUsingMultipleInputFiles) {
            IReadOnlyList<PackageSource> packageSources = wellKnownSources.GetSources();

            bool addDatabasePackageUrl = ShouldAddDatabasePackageUrl(groupEntry, packageSources);

            FSharpList<PackageSources.PackageSource> sources = group.Sources;

            // We created this file so remove the default NuGet sources
            if (createdNew && isMainGroup) {
                sources = RemoveSource(sources, Constants.OfficialNuGetUrlV3, Constants.OfficialNuGetUrl);
            }

            if (isMainGroup && !isUsingMultipleInputFiles) {
                if (group.Sources.Any(s => Equals(s.NuGetType, PackageSources.KnownNuGetSources.OfficialNuGetGallery))) {
                    sources = AddSource(Constants.OfficialNuGetUrlV3, sources);
                }
            }

            if (addDatabasePackageUrl) {
                sources = AddSource(Constants.DatabasePackageUri, sources);
            }

            // Upgrade any V2 to V3 sources
            if (sources.Any(s => Equals(s.NuGetType, PackageSources.KnownNuGetSources.OfficialNuGetGallery) && s.IsNuGetV2)) {
                sources = RemoveSource(sources, Constants.OfficialNuGetUrl);
                sources = AddSource(Constants.OfficialNuGetUrlV3, sources);
            }

            foreach (var source in packageSources) {
                sources = AddSource(source.Url, sources);
            }

            return sources;
        }

        private static InstallOptions CreateInstallOptions(IEnumerable<IDependencyRequirement> requirements, KeyValuePair<Domain.GroupName, DependenciesGroup> groupEntry, DependenciesGroup group, bool isMainGroup) {
            bool isStrict;
            if (isMainGroup) {
                isStrict = true;
            } else {
                isStrict = requirements.OfType<IDependencyGroup>()
                    .Where(s => s.DependencyGroup != null && string.Equals(s.DependencyGroup.GroupName, groupEntry.Key.Name, StringComparison.OrdinalIgnoreCase))
                    .Any(s => s.DependencyGroup.Strict);
            }

            InstallOptions options = group.Options;
            if (isStrict) {
                options = new InstallOptions(
                    true,
                    group.Options.Redirects,
                    group.Options.ResolverStrategyForDirectDependencies,
                    group.Options.ResolverStrategyForTransitives,
                    group.Options.Settings);
            }

            return options;
        }

        private bool ShouldAddDatabasePackageUrl(KeyValuePair<Domain.GroupName, DependenciesGroup> groupEntry, IReadOnlyList<PackageSource> packageSources) {
            // Only fallback to adding the database source if we are not using Azure hosted packages
            // This was needed as the private NuGet server could not handle the size of the database packages, but the Azure service can.
            if (packageSources.Any(s => string.Equals(s.Url, Constants.DatabasePackageUri, StringComparison.OrdinalIgnoreCase))) {
                foreach (var item in groupEntry.Value.Packages) {
                    if (string.Equals(item.Name.Name, "Aderant.Database.Backup", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }

            return false;
        }

        private FSharpList<PackageSources.PackageSource> RemoveSource(FSharpList<PackageSources.PackageSource> sources, params string[] urls) {
            foreach (var url in urls) {
                sources = ListModule.Except(sources.Where(s => string.Equals(s.Url, url, StringComparison.OrdinalIgnoreCase)), sources);
            }

            return sources;
        }

        private static FSharpList<PackageSources.PackageSource> AddSource(string source, FSharpList<PackageSources.PackageSource> sources) {
            if (sources.All(s => !string.Equals(s.Url, source, StringComparison.OrdinalIgnoreCase))) {
                sources = FSharpList<PackageSources.PackageSource>.Cons(PackageSources.PackageSource.NuGetV3Source(source), sources);
            }

            return sources;
        }

        private void AddModules(IEnumerable<IDependencyRequirement> requirements, bool validatePackageConstraints, ref DependenciesFile file) {
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
                        // Ignored.
                    }
                }

                if (!file.HasPackage(groupName, packageName)) {
                    try {
                        file = file.Add(groupName, packageName, version, Requirements.InstallSettings.Default);
                    } catch (Exception ex) {
                        if (requirement.VersionRequirement != null && requirement.VersionRequirement.OriginatingFile != null) {
                            string message = ex.Message;
                            if (!message.EndsWith(".")) {
                                message += ".";
                            }

                            throw new DependencyException(message + " The source file which caused the error was " + requirement.VersionRequirement.OriginatingFile);
                        }

                        throw;
                    }
                } else {
                    if (validatePackageConstraints) {
                        Requirements.PackageRequirement packageRequirement = file.GetPackage(groupName, packageName);

                        VersionStrategy versionStrategy = DependenciesFileParser.parseVersionString(version);
                        var requestedVersion = versionStrategy.VersionRequirement.FormatInNuGetSyntax();
                        var dependencyFileVersion = packageRequirement.VersionRequirement.FormatInNuGetSyntax();

                        if (requestedVersion != dependencyFileVersion) {
                            throw new DependencyException($"The package {packageName.name} in group {groupName.Name} has incompatible constraints defined ['{requestedVersion}' != '{dependencyFileVersion}']. Unify the constraints or move the package to a unique group.");
                        }
                    }
                }
            }
        }

        private bool HasLockFile() {
            return FileSystem.FileExists(dependencies.GetDependenciesFile().FindLockFile().FullName);
        }

        public void Restore(bool force = false) {
            Initialize();

            DoOperationAndHandleCredentialFailure(
                () => {
                    if (!HasLockFile()) {
                        new UpdateAction(dependencies, force).Run();
                        return;
                    }

                    new RestoreAction(dependencies, force).Run();
                });
        }

        private void DoOperationAndHandleCredentialFailure(Action action) {
            try {
                action();
            } catch (CredentialProviderUnknownStatusException ex) {
                logger.Error("A failure relating to credentials occurred.");
                logger.LogErrorFromException(ex, false, false);
                throw;
            }
        }

        public void Update(bool force) {
            DoOperationAndHandleCredentialFailure(
                () => { new UpdateAction(dependencies, force).Run(); });
        }

        public DependencyGroup GetDependencies() {
            return GetDependencies(Constants.MainDependencyGroup);
        }

        /// <summary>
        /// Returns the package requirements from the dependency file.
        /// Key: PackageName
        /// Value: Version constraint information
        /// </summary>
        public DependencyGroup GetDependencies(string groupName) {
            Initialize();

            var file = dependenciesFile;

            if (file == null) {
                file = dependencies.GetDependenciesFile();
            }

            var domainGroup = Domain.GroupName(groupName);
            var installOptions = file.GetGroup(domainGroup).Options;
            var restrictions = installOptions.Settings.FrameworkRestrictions;

            Requirements.FrameworkRestrictions.ExplicitRestriction restriction = null;

            if (!defaultInstallOptions.Settings.FrameworkRestrictions.Equals(restrictions)) {
                restriction = restrictions as Requirements.FrameworkRestrictions.ExplicitRestriction;
            }

            FSharpMap<Domain.PackageName, Paket.VersionRequirement> requirements = file.GetDependenciesInGroup(domainGroup);
            var map = requirements.ToDictionary(pair => pair.Key.ToString(), pair => NewRequirement(pair, file.FileName));

            var dependencyGroup = new DependencyGroup(groupName, map) {
                Strict = installOptions.Strict
            };

            if (restriction != null) {
                dependencyGroup.FrameworkRestrictions = restriction.Item.RepresentedFrameworks.Select(s => s.CompareString).ToList();
            }

            return dependencyGroup;
        }

        private VersionRequirement NewRequirement(KeyValuePair<Domain.PackageName, Paket.VersionRequirement> pair, string filePath) {
            List<string> prereleases = new List<string>();

            if (pair.Value.PreReleases.IsConcrete) {
                PreReleaseStatus.Concrete concrete = pair.Value.Item2 as PreReleaseStatus.Concrete;
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
                logger.Error($"Invalid version expression for requirement {pair.Key.Name} in file {filePath}. Does this requirement have any operators ('>', '<', '=') specified? Please fix the version.");
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
            if (!FileSystem.FileExists(Path.Combine(root, DependenciesFile))) {
                return new string[] {};
            }

            Initialize();
            Dependencies dependenciesFile = Dependencies.Locate(root);
            DependenciesFile file = dependenciesFile.GetDependenciesFile();

            return file.Groups.Select(s => s.Key.Name);
        }

        public void SetDependenciesFile(string lines) {
            Initialize();

            this.dependenciesFile = Paket.DependenciesFile.FromSource(this.root, lines);
            this.dependenciesFile.Save();
        }
    }
}