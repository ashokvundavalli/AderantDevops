using System;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;
using Aderant.Build.Packaging;
using Aderant.Build.Packaging.NuGet;
using Aderant.Build.Versioning;
using Microsoft.Build.Framework;

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

            Log.LogMessage("Processing folder: {0}", Folder);

            if (IsModified(Folder)) {
            }

            Version version = GetVersion();

            UpdateSpecification(version);

            return !Log.HasLoggedErrors;
        }

        private bool IsModified(string folder) {
            var diff = new PackageDifferencer(fileSystem, logger);
            diff.IsModified(Path.GetFileName(folder), folder);

            return false;
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

            fileSystem.AddFile(specFilePath, stream => {
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

            fs.AddFile(specFile, stream => {
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