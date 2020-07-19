using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.Packaging.NuGet;
using Aderant.Build.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ILogger = Aderant.Build.Logging.ILogger;
using Nuspec = Aderant.Build.Packaging.NuGet.Nuspec;
using Task = Microsoft.Build.Utilities.Task;
using VersionRequirement = Aderant.Build.DependencyResolver.VersionRequirement;

namespace Aderant.Build.Tasks {
    public sealed class ThirdPartyPackager : Task {
        private IFileSystem2 fileSystem;
        private BuildTaskLogger logger;

        public ThirdPartyPackager() {
            VersionByTimestamp = true;
            this.logger = new BuildTaskLogger(this);
        }

        /// <summary>
        /// A map of packages and nuspec files to allow for parallel processing.
        /// </summary>
        public ITaskItem[] PackageMap { get; set; }

        public string Folder { get; set; }

        /// <summary>
        /// An optional nuspec file to control packaging.
        /// </summary>
        public string[] NuspecFile { get; set; } = Array.Empty<string>();

        public bool VersionByTimestamp { get; set; }

        public bool EnableVerboseLogging { get; set; }

        public override bool Execute() {
            SingleFolderPackager.EnableVerboseLogging = EnableVerboseLogging;

            // Convert legacy callers to the package map.
            if (PackageMap == null || PackageMap.Length == 0) {
                if (Folder != null) {
                    PackageMap = new ITaskItem[] {
                        new TaskItem(
                            Folder,
                            new Hashtable {
                                { "NuspecFile", string.Join(";", NuspecFile) }
                            }),
                    };
                }
            }

            // Clear input values to prevent further usage and thus bugs
            NuspecFile = null;
            Folder = null;

            fileSystem = new PhysicalFileSystem();

            Parallel.ForEach(
                PackageMap,
                item => {
                    var metadata = item.GetMetadata("NuspecFile");

                    string[] nuspecFiles = Array.Empty<string>();

                    if (!string.IsNullOrEmpty(metadata)) {
                        nuspecFiles = metadata.Split(';');
                    }

                    PackageFolder(item.ItemSpec, nuspecFiles);
                });

            return !Log.HasLoggedErrors;
        }

        private void PackageFolder(string folder, string[] nuspecFile) {
            if (!fileSystem.DirectoryExists(folder)) {
                return;
            }

            // Ignore folder names like $tf, .git, etc. which cause problems.
            string[] ignoredPrefixes = new[] {
                "$",
                "_",
                "."
            };

            var packageName = Path.GetFileName(folder);

            if (ignoredPrefixes.Any(prefix => packageName.StartsWith(prefix))) {
                Log.LogMessage($"Ignoring folder name {packageName}.");
                return;
            }

            if (nuspecFile.Length > 1) {
                foreach (var file in nuspecFile) {
                    if (SingleFolderPackager.IgnoreNuspec(file)) {
                        continue;
                    }

                    new SingleFolderPackager(fileSystem, logger).PackageSingleFolder(Path.GetDirectoryName(file), file);
                }
            } else {
                new SingleFolderPackager(fileSystem, logger).PackageSingleFolder(folder, nuspecFile.FirstOrDefault());
            }
        }

        private class SingleFolderPackager {
            private static object syncLock = new object();

            internal static string TemporaryPackagingDirectoryName = "package_temp";

            private readonly IFileSystem2 fileSystem;
            private readonly ILogger logger;

            public SingleFolderPackager(IFileSystem2 fileSystem, ILogger logger) {
                this.fileSystem = fileSystem;
                this.logger = logger;
            }

            public static bool EnableVerboseLogging { get; set; }

            public ILogger Log {
                get { return logger; }
            }

            public void PackageSingleFolder(string folder, string nuspecFile) {
                if (SingleFolderPackager.IgnoreNuspec(nuspecFile)) {
                    nuspecFile = null;
                }

                LogMessage("Processing folder: {0}", folder);

                var packageTemp = Path.Combine(folder, TemporaryPackagingDirectoryName);

                if (!IsModified(folder, nuspecFile, packageTemp, out packageTemp)) {
                    LogMessage("No changes where detected for {0}. It will be excluded from packaging.", folder);

                    IEnumerable<string> files = fileSystem.GetFiles(folder, "*.nuspec", true);
                    foreach (var file in files) {
                        logger.Info("Removing file " + file);
                        fileSystem.DeleteFile(file);
                    }

                    if (fileSystem.DirectoryExists(packageTemp)) {
                        fileSystem.DeleteDirectory(packageTemp, true);
                    }

                    return;
                }

                Version version = GetVersion(folder);

                UpdateSpecification(version, folder, nuspecFile, packageTemp);
            }

            private bool IsModified(string folder, string nuspecFile, string packageTemp, out string existingPackageDirectory) {
                ReadSpecification(folder, nuspecFile, out string packageVersion, out string packageName);

                bool hasUserDefinedVersion = !string.IsNullOrEmpty(packageVersion);

                if (DownloadPackage(packageName, packageVersion, packageTemp)) {
                    existingPackageDirectory = Path.Combine(packageTemp, "packages", packageName);

                    PackageComparer comparer = new PackageComparer(fileSystem, logger);

                    // Due to parallel processing take a lock to ensure any difference logging is emitted unbroken
                    lock (syncLock) {
                        if (comparer.HasChanges(existingPackageDirectory, folder, nuspecFile)) {
                            if (hasUserDefinedVersion) {
                                throw new InvalidOperationException(string.Format("Cannot package. The version {0} of {1} already exists but changes have been detected. A new version must be specified.", packageVersion, packageName));
                            }

                            return true;
                        }

                        return false;
                    }
                }

                existingPackageDirectory = null;
                return true;
            }

            private void ReadSpecification(string folder, string nuspecFile, out string packageVersion, out string packageName) {
                if (!string.IsNullOrEmpty(nuspecFile)) {
                    // We have a specification file so use the version and name provided.
                    var specificationText = ReadSpecFile(nuspecFile);
                    var specification = new Nuspec(specificationText);

                    // Ensure .nuspec files node is present.
                    if (specification.Files?.Value == null) {
                        throw new Exception($"Nuspec file: '{nuspecFile}' is missing required files node. See https://docs.microsoft.com/en-us/nuget/reference/nuspec#files for details.");
                    }

                    packageName = specification.Id.Value;
                    packageVersion = specification.Version.Value;
                    return;
                }

                packageName = Path.GetFileName(folder);
                packageVersion = null;
            }

            private bool DownloadPackage(string packageName, string packageVersion, string packageTemp) {
                logger.Info("Downloading package: " + packageName + " Version: " + packageVersion + " Package Temp Directory: " + packageTemp);

                if (string.IsNullOrEmpty(packageVersion)) {
                    // Default download version.
                    packageVersion = ">= 1.0.0 ci";
                }

                // Download the existing package.
                try {
                    using (PaketPackageManager packageManager = new PaketPackageManager(packageTemp, fileSystem, WellKnownPackageSources.Default, logger, EnableVerboseLogging)) {
                        var requirement = DependencyRequirement.Create(
                            packageName,
                            Constants.MainDependencyGroup,
                            new VersionRequirement {
                                ConstraintExpression = string.Format("{0}", packageVersion)
                            });
                        requirement.ReplaceVersionConstraint = true;

                        packageManager.Add(new[] { requirement });
                        packageManager.Update(true);
                    }
                } catch (Exception ex) {
                    while (ex != null) {
                        if (ex.Message.StartsWith("Unable to retrieve package details") ||
                            ex.Message.StartsWith("Unable to retrieve package versions for") ||
                            ex.Message.StartsWith("Could not find versions for package")) {
                            logger.Warning("Package {0} doesn't exist. Assuming it is a new package.", packageName);
                            return false;
                        }

                        ex = ex.InnerException;
                    }

                    logger.Error(ex.Message);
                    throw;
                }

                return true;
            }

            private void UpdateSpecification(Version newVersion, string folder, string nuspecFile, string existingPackageDirectory) {
                string specificationFilePath = nuspecFile;

                if (string.IsNullOrEmpty(specificationFilePath)) {
                    if (existingPackageDirectory != null) {
                        ValidateVersionNewer(newVersion, existingPackageDirectory);
                    }

                    specificationFilePath = CreateSpecFile(folder);
                }

                LogMessage("Located specification file: {0}", specificationFilePath);

                string specificationText = ReadSpecFile(specificationFilePath);

                UpdateSpecification(folder, specificationFilePath, specificationText, newVersion);
            }

            /// <summary>
            /// Ensures that the calculated version is higher than the version of the package that was downloaded
            /// </summary>
            private void ValidateVersionNewer(Version version, string existingPackageDirectory) {
                var existingSpecificationFilePath = fileSystem.GetFiles(existingPackageDirectory, "*.nuspec", false).FirstOrDefault();

                string existingVersion;
                string packageName;
                ReadSpecification(null, existingSpecificationFilePath, out existingVersion, out packageName);

                if (!SemanticVersion.IsPreRelease(existingVersion)) {
                    var semVer = Version.Parse(existingVersion);

                    if (version <= semVer) {
                        throw new InvalidOperationException(string.Format("Package {0} must have a higher version as it has different contents. Existing version: {1} New version: {2}. The tool cannot determine a good enough version to use. Specifying a version via a nuspec file is recommended.", packageName, existingVersion, version));
                    }
                }
            }

            /// <summary>
            /// Calculates a version for a package. If no version can be calculated then null is returned.
            /// </summary>
            private Version GetVersion(string folder) {
                VersionAnalyzer analyzer = new VersionAnalyzer(logger, fileSystem);
                analyzer.Analyzer = new FileVersionAnalyzer();
                Version version = analyzer.Execute(folder);

                if (version != null) {
                    LogMessage("The assembly or file version for {0} is {1}", folder, version.ToString());
                    return version;
                }

                return null;
            }

            private void UpdateSpecification(string folder, string specFilePath, string specFileText, Version version) {
                Nuspec specification = new Nuspec(specFileText);
                if (specification.Id.IsVariable) {
                    specification.Id.Value = Path.GetFileName(folder);
                }

                if (specification.Version.IsVariable) {
                    var prereleaseMoniker = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                    specification.Version.Value = string.Format("{0}.{1}.{2}-ci-{3}", version.Major, version.Minor, version.Build, prereleaseMoniker);
                    LogMessage("Package version is: " + specification.Version.Value);
                }

                if (specification.Description.HasReplacementTokens) {
                    specification.Description.ReplaceToken(WellKnownTokens.Id, specification.Id.Value);
                }

                string text = specification.Save();

                // Guard for TFS which uses read-only files
                fileSystem.MakeFileWritable(specFilePath);

                fileSystem.AddFile(
                    specFilePath,
                    stream => {
                        using (stream) {
                            using (var writer = new StreamWriter(stream)) {
                                writer.Write(text);
                            }
                        }
                    });
            }

            private string ReadSpecFile(string specFile) {
                using (var reader = new StreamReader(fileSystem.OpenFile(specFile))) {
                    return reader.ReadToEnd();
                }
            }

            private string CreateSpecFile(string folder) {
                LogMessage("Specification file does not exist, creating...");

                string specFile = Path.Combine(folder, Path.GetFileName(folder) + ".nuspec");

                fileSystem.AddFile(
                    specFile,
                    stream => {
                        using (stream) {
                            using (StreamWriter writer = new StreamWriter(stream)) {
                                writer.Write(Resources.TemplateNuspec);
                            }
                        }
                    });

                return specFile;
            }

            private void LogMessage(string message, params object[] args) {
                Log.Info(message, args);
            }

            public static bool IgnoreNuspec(string nuspecFile) {
                if (nuspecFile != null && nuspecFile.IndexOf(Path.DirectorySeparatorChar + TemporaryPackagingDirectoryName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) {
                    return true;
                }

                return false;
            }
        }
    }
}