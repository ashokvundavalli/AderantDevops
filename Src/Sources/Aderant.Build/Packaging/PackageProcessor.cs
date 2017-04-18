using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management.Automation.Host;
using Aderant.Build.Logging;
using Aderant.Build.TeamFoundation;

namespace Aderant.Build.Packaging {
    public class PackageProcessor {
        private PowerShellLogger logger;

        public PackageProcessor(object host) {
            if (host is PSHostUserInterface) {
                this.logger = new PowerShellLogger(host as PSHostUserInterface);
            }
        }

        public void AssociatePackagesToBuild(FileInfo[] packages) {
            TfBuildCommands commands = new TfBuildCommands(logger);

            foreach (var package in packages) {
                using (ZipArchive archive = ZipFile.OpenRead(package.FullName)) {
                    ZipArchiveEntry entry = archive.Entries.FirstOrDefault(e => e.FullName.IndexOf(".nuspec", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (entry != null) {
                        using (Stream stream = entry.Open()) {
                            using (StreamReader reader = new StreamReader(stream)) {
                                var nuspec = new NuGet.Nuspec(reader.ReadToEnd());

                                string name = nuspec.Id.Value;
                                string nuspecVersion = nuspec.Version.Value;

                                if (string.IsNullOrWhiteSpace(name)) {
                                    throw new ArgumentNullException(nameof(name), "Package name is null or whitespace");
                                }

                                if (string.IsNullOrWhiteSpace(nuspecVersion)) {
                                    throw new ArgumentNullException(nameof(nuspecVersion), "Package version is null or whitespace");
                                }

                                commands.LinkArtifact(name, TfBuildArtifactType.FilePath, $@"\\dfs.aderant.com\PackageRepository\{name}\{nuspecVersion}");
                            }
                        }
                    }
                }
            }
        }
    }
}