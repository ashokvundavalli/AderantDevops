using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Tasks {
    [TestClass]
    [DeploymentItem("IntegrationTest.targets")]
    public abstract class BuildTaskTestBase {
        public TestContext TestContext { get; set; }

        protected void RunTarget(string targetName) {
            string buildTool = ToolLocationHelper.GetPathToBuildTools("14.0");

            StringBuilder sb = new StringBuilder();
            sb.Append("\"" + Path.Combine(TestContext.DeploymentDirectory, "IntegrationTest.targets") + "\"");
            sb.Append(" ");
            sb.Append("/p:NoMSBuildCommunityTasks=true");
            sb.Append(" ");
            sb.Append("/p:BuildToolsDirectory=" + "\"" + TestContext.DeploymentDirectory + "\"");
            sb.Append(" ");
            sb.Append("/Target:" + targetName);

            ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(buildTool, "MSBuild.exe"), sb.ToString());

            int exitCode = StartProcessAndWaitForExit(startInfo, (args, isError, process) => {
                if (args.Data != null)
                    TestContext.WriteLine(args.Data);
            });

            Assert.AreEqual(0, exitCode, "Target " + targetName + "has failed");
        }

        private static int StartProcessAndWaitForExit(ProcessStartInfo startInfo, Action<DataReceivedEventArgs, bool, Process> onReceiveStandardErrorOrOutputData) {
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            var process = new Process {
                StartInfo = startInfo
            };

            process.ErrorDataReceived += (sender, e) => onReceiveStandardErrorOrOutputData(e, true, sender as System.Diagnostics.Process);
            process.OutputDataReceived += (sender, e) => onReceiveStandardErrorOrOutputData(e, false, sender as System.Diagnostics.Process);
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit();

            return process.ExitCode;
        }
    }
}