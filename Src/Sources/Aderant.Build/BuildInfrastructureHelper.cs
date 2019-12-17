using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Aderant.Build {
    internal static class BuildInfrastructureHelper {

        internal static string PathToBuildScriptsFromModules = Path.Combine(BuildConstants.BuildInfrastructureDirectory, "Src", "Build");

        internal static string PathToBuildToolsFromModules = Path.Combine(BuildConstants.BuildInfrastructureDirectory, "Src", "Build.Tools");

        internal static string GetPathToThirdPartyModules(string branchModulesDirectory, string branchName) {
            var branchDirectory = Path.Combine(branchModulesDirectory, @"..\");
            var subDirectoriesToGoUp = branchName.Split('\\').Length;
            var expertSuiteDirectory = branchDirectory;
            for (int i = 0; i < subDirectoriesToGoUp; i++) {
                expertSuiteDirectory = Path.Combine(expertSuiteDirectory, @"..\");
            }
            var thirdPartyFolder = Path.Combine(expertSuiteDirectory, "ThirdParty");
            return thirdPartyFolder;
        }

        internal static Task StartProcessAsync(
            string processFilePath, 
            string arguments, 
            string workingDirectory, 
            Action<DataReceivedEventArgs, bool, System.Diagnostics.Process> onReceiveStandardErrorOrOutputData) {
            
            return Task.Factory.StartNew(() => {
                StartProcessAndWaitForExit(processFilePath, arguments, workingDirectory, onReceiveStandardErrorOrOutputData);
            });
        }

        internal static void StartProcessAndWaitForExit(
            string processFilePath, 
            string arguments, 
            string workingDirectory, 
            Action<DataReceivedEventArgs, bool, System.Diagnostics.Process> onReceiveStandardErrorOrOutputData) {

            var processStartInfo = new ProcessStartInfo(processFilePath, arguments) {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var process = new System.Diagnostics.Process {
                StartInfo = processStartInfo
            };
            process.ErrorDataReceived += (sender, e) => onReceiveStandardErrorOrOutputData(e, true, sender as System.Diagnostics.Process);
            process.OutputDataReceived += (sender, e) => onReceiveStandardErrorOrOutputData(e, false, sender as System.Diagnostics.Process);
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process?.WaitForExit();
        }
    }
}