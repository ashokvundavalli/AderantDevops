using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.Packaging.NuGet;
using Aderant.Build.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class ThirdPartyPackager : Microsoft.Build.Utilities.Task {
        private IFileSystem2 fileSystem;
        private BuildTaskLogger logger;

        public ThirdPartyPackager() {
            VersionByTimestamp = true;
            this.logger = new BuildTaskLogger(this);
        }

        [Required]
        public string Folder { get; set; }

        public bool VersionByTimestamp { get; set; }

        public override bool Execute() {
            fileSystem = new PhysicalFileSystem(Folder);

            if (!fileSystem.DirectoryExists(Folder)) {
                return !Log.HasLoggedErrors;
            }

            // Ignore folder names like $tf, .git, etc. which cause problems.
            var packageName = Path.GetFileName(Folder) ?? "";
            if (packageName.StartsWith("$") || packageName.StartsWith(".")) {
                Log.LogMessage($"Ignoring strange folder name {packageName}.");
                return !Log.HasLoggedErrors;
            }

            Log.LogMessage("Processing folder: {0}", Folder);

            if (!IsModified(Folder)) {
                Log.LogMessage("No changes where detected for {0}. It will be excluded from packaging.", Folder);

                IEnumerable<string> files = fileSystem.GetFiles(Folder, "*.nuspec", true);
                foreach (var file in files) {
                    logger.Info("Removing file " + file);
                    fileSystem.DeleteFile(file);
                }
                return !Log.HasLoggedErrors;
            }

            Version version = GetVersion();

            UpdateSpecification(version);

            return !Log.HasLoggedErrors;
        }

        private bool IsModified(string folder) {
            var packageName = Path.GetFileName(folder);
            if (DownloadPackage(packageName)) {

                string existingPackageDirectory = Path.Combine(folder, "packages", packageName);

                PackageComparer comparer = new PackageComparer(fileSystem, logger);
                return comparer.HasChanges(existingPackageDirectory, folder);
            }

            return true;
        }

        private bool DownloadPackage(string packageName) {
            // Download the existing package
            try {
                using (PackageManager packageManager = new PackageManager(fileSystem, logger)) {
                    var requirement = DependencyRequirement.Create(
                        packageName,
                        BuildConstants.MainDependencyGroup,
                        new VersionRequirement {
                            ConstraintExpression = ">= 0.0.0 ci"
                        });
                    packageManager.Add(new[] { DependencyRequirement.Create(packageName, BuildConstants.MainDependencyGroup) });

                    packageManager.Add(new[] { requirement });
                    packageManager.Update(true);
                    packageManager.Restore(true);
                }
            } catch (Exception ex) {
                if (ex.Message.Contains("Could not find versions for package") || ex.Message.StartsWith("Unable to retrieve package versions")) {
                    logger.Warning("Package {0} doesn't exist. Assuming it is a new package.", packageName);

                    return false;
                } else {
                    logger.Error(ex.Message);
                }
            }

            return true;
        }

        private void UpdateSpecification(Version version) {
            string specificationFilePath = fileSystem.GetFiles(Folder, "*.nuspec", true).FirstOrDefault(p => !p.Contains("packages"));

            if (specificationFilePath == null) {
                specificationFilePath = CreateSpecFile(fileSystem);
            } else {
                Log.LogMessage("Located specification file: {0}", specificationFilePath);
            }

            string specificationText = ReadSpecFile(fileSystem, specificationFilePath);

            UpdateSpecification(specificationFilePath, specificationText, version);
        }

        private Version GetVersion() {
            VersionAnalyzer analyzer = new VersionAnalyzer(logger, fileSystem);
            analyzer.Analyzer = new FileVersionAnalyzer();
            Version version = analyzer.Execute(Folder);

            Log.LogMessage("The assembly or file version for {0} is {1}", Folder, version.ToString());
            return version;
        }

        private void UpdateSpecification(string specFilePath, string specFileText, Version version) {
            Nuspec specification = new Nuspec(specFileText);
            if (specification.Id.IsVariable) {
                specification.Id.Value = Path.GetFileName(Folder);
            }

            if (specification.Version.IsVariable) {
                var prereleaseMoniker = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                specification.Version.Value = string.Format("{0}.{1}.{2}-ci-{3}", version.Major, version.Minor, version.Build, prereleaseMoniker);
                Log.LogMessage("Full package version is: " + specification.Version.Value);
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

        private static string ReadSpecFile(IFileSystem2 fileSystem, string specFile) {
            using (var reader = new StreamReader(fileSystem.OpenFile(specFile))) {
                return reader.ReadToEnd();
            }
        }

        private string CreateSpecFile(IFileSystem2 fs) {
            Log.LogMessage("Specification file does not exist, creating...");

            string specFile = Path.Combine(Folder, Path.GetFileName(Folder) + ".nuspec");

            fs.AddFile(
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
    }
}