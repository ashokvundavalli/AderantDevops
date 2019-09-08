using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management.Automation.Host;
using Aderant.Build.AzurePipelines;
using Aderant.Build.Logging;

namespace Aderant.Build.Packaging {
    public class PackageProcessor {
        private PowerShellLogger logger;

        public PackageProcessor(object host) {
            if (host is PSHostUserInterface) {
                this.logger = new PowerShellLogger(host as PSHostUserInterface);
            }
        }

        public void UpdateBuildNumber(string buildNumber) {
            VsoCommandBuilder commandBuilder = new VsoCommandBuilder(logger);
            commandBuilder.UpdateBuildNumber(buildNumber);
        }

        public void AssociatePackagesToBuild(FileInfo[] packages) {
            VsoCommandBuilder commandBuilder = new VsoCommandBuilder(logger);

            foreach (var package in packages) {
                using (ZipArchive archive = ZipFile.OpenRead(package.FullName)) {
                    ZipArchiveEntry entry = archive.Entries.FirstOrDefault(e => e.FullName.IndexOf(".nuspec", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (entry != null) {
                        using (Stream stream = entry.Open()) {
                            using (StreamReader reader = new StreamReader(stream)) {
                                AssociatePackageToBuild(reader.ReadToEnd(), commandBuilder);
                            }
                        }
                    }
                }
            }
        }

        // TODO: Remove this once the we are not longer pushing to packages.ap.aderant.com and have garbage collection in place for Azure
        internal void AssociatePackageToBuild(string nuspecText, VsoCommandBuilder vsoCommandsBuilder) {
            var nuspec = new NuGet.Nuspec(nuspecText);

            string name = nuspec.Id.Value;
            string nuspecVersion = nuspec.Version.Value;

            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentNullException(nameof(name), "Package name is null or whitespace");
            }

            if (string.IsNullOrWhiteSpace(nuspecVersion)) {
                throw new ArgumentNullException(nameof(nuspecVersion), "Package version is null or whitespace");
            }

            // Damn build systems. So you would think that TFS would take the path verbatim and just store that away.
            // But no, it takes the UNC path you give it and then when the garbage collection occurs it appends the artifact name as a folder
            // to that original path as the final path to delete.
            // This means the web UI for a build will always point to the root folder, which is useless for usability and we need to
            // set the actual final folder as the name.
            // Aderant.Database.Backup
            if (name.Equals("Aderant.Database.Backup")) {
                vsoCommandsBuilder.LinkArtifact($"{name}\\{nuspecVersion}", VsoBuildArtifactType.FilePath, Constants.DatabasePackageUri);
            } else {
                vsoCommandsBuilder.LinkArtifact($"{name}\\{nuspecVersion}", VsoBuildArtifactType.FilePath, Constants.PackageRepositoryUri);
            }
        }
    }
}
