using System.IO;
using System.Management.Automation.Host;
using Aderant.Build.AzurePipelines;
using Aderant.Build.Logging;

namespace Aderant.Build.Packaging {
    public class PackageProcessor {
        private ILogger logger;

        public PackageProcessor(PSHost host) {
            if (host == null) {
                logger = NullLogger.Default;
            } else {
                logger = PowerShellLogger.Create(host.UI);
            }
        }

        public void UpdateBuildNumber(string buildNumber) {
            VsoCommandBuilder commandBuilder = new VsoCommandBuilder(logger);
            commandBuilder.UpdateBuildNumber(buildNumber);
        }

        public void AssociatePackagesToBuild(FileInfo[] packages) {
        }
    }
}
