using System.Management.Automation.Host;
using Aderant.Build.Logging;
using Aderant.Build.TeamFoundation;
using Paket;

namespace Aderant.Build.Packaging {
    public class PackageProcessor {
        private PowerShellLogger logger;

        public PackageProcessor(object host) {
            if (host is PSHostUserInterface) {
                this.logger = new PowerShellLogger(host as PSHostUserInterface);
            }
        }

        public void AssociatePackagesToBuild(string[] packages) {
            TfBuildCommands commands = new TfBuildCommands(logger);

            foreach (var package in packages) {
                Nuspec nuspec = Paket.Nuspec.Load(package);

                string name = nuspec.OfficialName;
                string nuspecVersion = nuspec.Version;

                commands.LinkArtifact(nuspecVersion, TfBuildArtifactType.FilePath, @"\\dfs.aderant.com\PackageRepository\" + name);
            }
        }
    }
}