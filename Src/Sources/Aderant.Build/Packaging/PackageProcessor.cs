using System.Management.Automation.Host;
using Aderant.Build.Logging;
using Aderant.Build.TeamFoundation;
using Paket;

namespace Aderant.Build.Packaging {
    public class PackageProcessor {
        private readonly IFileSystem2 fileSystem2;
        private PowerShellLogger logger;

        public PackageProcessor(object host, string repositoryPath)
            : this(new PhysicalFileSystem(repositoryPath)) {
            if (host is PSHostUserInterface) {
                this.logger = new PowerShellLogger(host as PSHostUserInterface);
            }
        }

        private PackageProcessor(IFileSystem2 fileSystem2) {
            this.fileSystem2 = fileSystem2;
        }

        public void AssociatePackagesToBuild(string[] packages) {
            TfBuildCommands commands = new TfBuildCommands(logger);

            foreach (var package in packages) {
                Nuspec nuspec = Paket.Nuspec.Load(package);

                string name = nuspec.OfficialName;
                string nuspecVersion = nuspec.Version;
            }

            commands.LinkArtifact("Drop", TfBuildArtifactType.FilePath, @"\\dfs.aderant.com\packages\MichaelTest\A");
        }
    }
}