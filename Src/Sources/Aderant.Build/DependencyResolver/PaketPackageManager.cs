using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Aderant.Build.DependencyResolver.Model;
using Aderant.Build.Logging;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
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

        public PaketPackageManager(string root, IFileSystem fileSystem, IWellKnownSources wellKnownSources, ILogger logger, bool enableVerboseLogging = false) {
            this.root = root;
            this.wellKnownSources = wellKnownSources;
            this.logger = logger;
            FileSystem = fileSystem;

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
        public static string DependenciesFile { get; } = Constants.PaketDependencies;

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
                if (loggerReference.TryGetTarget(out var target)) {
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
                if (args.Text.StartsWith("Could not detect any platforms from")) {
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

            // Indicates if the system is resolving for a single directory or multiple directories.
            bool isUsingMultipleInputFiles = resolverRequest != null && resolverRequest.Modules.Count() > 1;

            AddModules(requirements, validatePackageConstraints, ref dependenciesFile);

            FileSystem.MakeFileWritable(dependenciesFile.FileName);

            var groups = dependenciesFile.Groups;

            var groupList = new List<DependenciesGroup>();

            foreach (var groupEntry in groups) {
                var group = dependenciesFile.GetGroup(groupEntry.Key);

                bool isMainGroup = string.Equals(group.Name.Name, Constants.MainDependencyGroup, StringComparison.OrdinalIgnoreCase);

                var sources = CreateSources(group, isMainGroup, isUsingMultipleInputFiles);
                var options = CreateInstallOptions(requirements, groupEntry, group, isMainGroup);

                var requiredRemoteFiles = requirements.Where(s => string.Equals(s.Group, group.Name.Name, StringComparison.OrdinalIgnoreCase)).OfType<RemoteFile>().ToList();

                // Paket cannot add new remote files via its internal API so we need to process these last otherwise the will vanish
                // from the internal data structures if done inside AddModules
                var remoteFileList = MergeRemoteFiles(requiredRemoteFiles);

                var newGroup = new DependenciesGroup(
                    group.Name,
                    sources,
                    group.Caches,
                    options,
                    group.Packages,
                    group.ExternalLocks,
                    remoteFileList);

                if (isMainGroup) {
                    groupList.Insert(0, newGroup);
                } else {
                    groupList.Add(newGroup);
                }
            }

            var fileWriter = new DependenciesFileWriter();
            var result = fileWriter.Write(groupList, resolverRequest?.GetFrameworkRestrictions());

            var map = MapModule.Empty<Domain.GroupName, DependenciesGroup>();
            foreach (var group in groupList) {
                map = map.Add(group.Name, group);
            }

            dependenciesFile = new DependenciesFile(
                dependenciesFile.FileName,
                map,
                result.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));

            dependenciesFile.Save();

            Lines = dependenciesFile.Lines;
        }

        private FSharpList<PackageSources.PackageSource> CreateSources(DependenciesGroup group, bool isMainGroup, bool isUsingMultipleInputFiles) {
            IReadOnlyList<PackageSource> packageSources = wellKnownSources.GetSources();

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
            var installSettings = Requirements.InstallSettings.Default;

            foreach (var requirement in requirements.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)) {
                var hasCustomVersion = false;
                var version = string.Empty;

                if (requirement.VersionRequirement != null && !string.IsNullOrWhiteSpace(requirement.VersionRequirement.ConstraintExpression)) {
                    hasCustomVersion = true;
                    version = requirement.VersionRequirement.ConstraintExpression;
                }

                var groupName = Domain.GroupName(requirement.Group);

                if (requirement is RemoteFile) {
                    continue;
                }

                var packageName = Domain.PackageName(requirement.Name);
                if (requirement.ReplaceVersionConstraint && hasCustomVersion) {
                    try {
                        file = file.Remove(Domain.GroupName(requirement.Group), packageName);
                    } catch {
                        // Ignored.
                    }
                }

                if (!file.HasPackage(groupName, packageName)) {
                    try {
                        file = file.Add(groupName, packageName, version, installSettings);
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
                        // This is inefficient as it walks the input set many times but the input length is short enough
                        // that it won't be a performance bottleneck
                        if (requirements.Count(s => string.Equals(s.Group, requirement.Group, StringComparison.OrdinalIgnoreCase) && string.Equals(s.Name, requirement.Name, StringComparison.OrdinalIgnoreCase)) > 1) {
                            var packageRequirement = file.GetPackage(groupName, packageName);

                            var versionStrategy = DependenciesFileParser.parseVersionString(version);
                            var requestedVersion = versionStrategy.VersionRequirement.FormatInNuGetSyntax();
                            var dependencyFileVersion = packageRequirement.VersionRequirement.FormatInNuGetSyntax();

                            if (requestedVersion != dependencyFileVersion) {
                                throw new DependencyException($"The package {packageName.name} in group {groupName.Name} has incompatible constraints defined ['{requestedVersion}' != '{dependencyFileVersion}']. Unify the constraints or move one of the packages to a unique group.");
                            }
                        }
                    }

                    // If a group has only one package then Remove() will remove the group and
                    // we loose the group settings so we need to take a copy first
                    var packageGroup = file.GetGroup(groupName);
                    InstallOptions options = packageGroup.Options;

                    file = file.Remove(groupName, packageName);
                    file = file.Add(groupName, packageName, version, options.Settings);
                }
            }
        }

        /// <summary>
        /// Adds a new remote file to the dependency file
        /// </summary>
        /// <param name="newRemoteFiles">The group to place the file into</param>
        private static FSharpList<ModuleResolver.UnresolvedSource> MergeRemoteFiles(List<RemoteFile> newRemoteFiles) {
            return ListModule.OfSeq(
                newRemoteFiles.Select(s => new ModuleResolver.UnresolvedSource(
                    "",
                    "",
                    s.ItemName,
                    ModuleResolver.Origin.NewHttpLink(s.Name),
                    ModuleResolver.VersionRestriction.NoVersionRestriction,
                    FSharpOption<string>.None,
                    FSharpOption<string>.None,
                    FSharpOption<string>.None,
                    FSharpOption<string>.None)));
        }

        private bool HasLockFile() {
            return FileSystem.FileExists(dependencies.GetDependenciesFile().FindLockFile().FullName);
        }

        public void Restore(bool force = false, CancellationToken cancellationToken = default(CancellationToken)) {
            Initialize();

            DoOperationAndHandleCredentialFailure(
                () => {
                    if (!HasLockFile()) {
                        new UpdateAction(dependencies, force).Run(cancellationToken);
                        return;
                    }

                    new RestoreAction(dependencies, force).Run(cancellationToken);
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

        public void Update(bool force, CancellationToken cancellationToken = default(CancellationToken)) {
            DoOperationAndHandleCredentialFailure(
                () => { new UpdateAction(dependencies, force).Run(cancellationToken); });
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

            var requirements = file.GetDependenciesInGroup(domainGroup);
            var remoteFiles = file.Groups[domainGroup].RemoteFiles;

            var map = requirements.ToDictionary(pair => pair.Key.ToString(), pair => NewRequirement(pair, file.FileName));

            var dependencyGroup = new DependencyGroup(groupName, map) {
                Strict = installOptions.Strict, RemoteFiles = RemoteFileMapper.Map(remoteFiles, groupName).ToList()
            };

            if (restriction != null) {
                dependencyGroup.FrameworkRestrictions = restriction.Item.RepresentedFrameworks.Select(s => s.CompareString).ToList();
            }

            return dependencyGroup;
        }

        private VersionRequirement NewRequirement(KeyValuePair<Domain.PackageName, Paket.VersionRequirement> pair, string filePath) {
            List<string> prereleases = new List<string>();

            if (pair.Value.PreReleases.IsConcrete) {
                var concrete = pair.Value.Item2 as PreReleaseStatus.Concrete;
                if (concrete != null) {
                    prereleases.Add(concrete.Item.HeadOrDefault);

                    var item = concrete.Item.TailOrNull;
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
                OriginatingFile = filePath, ConstraintExpression = $"{expression} {string.Join(" ", prereleases)}",
            };
        }

        public IEnumerable<string> FindGroups() {
            if (!FileSystem.FileExists(Path.Combine(root, DependenciesFile))) {
                return new string[] {
                };
            }

            Initialize();
            Dependencies dependenciesFile = Dependencies.Locate(root);
            DependenciesFile file = dependenciesFile.GetDependenciesFile();

            return file.Groups.Select(s => s.Key.Name);
        }

        public void SetDependenciesFile(string lines) {
            Initialize();

            dependenciesFile = Paket.DependenciesFile.FromSource(root, lines);
            dependenciesFile.Save();
        }

        internal class RemoteFileMapper {
            public static RemoteFile Map(ModuleResolver.UnresolvedSource remoteFile, string groupName) {
                return Map(new[] { remoteFile }, groupName).FirstOrDefault();
            }

            public static IEnumerable<RemoteFile> Map(IEnumerable<ModuleResolver.UnresolvedSource> remoteFiles, string groupName) {
                foreach (var item in remoteFiles) {
                    string itemName = item.Name;
                    string uri = item.ToString();

                    // Paket appends "http " and gives us no way to get the raw URL so we need to do some pruning
                    uri = TrimHttpPrefix(TrimHttpPrefix(uri, "https "), "http ");
                    uri = uri.TrimEnd(itemName.ToCharArray()).TrimEnd();

                    yield return new RemoteFile(itemName, uri, groupName);
                }
            }

            private static string TrimHttpPrefix(string uri, string prefix) {
                var pos = uri.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (pos >= 0 && uri.Length > pos) {
                    uri = uri.Remove(pos, prefix.Length);
                }

                return uri;
            }
        }
    }
}
