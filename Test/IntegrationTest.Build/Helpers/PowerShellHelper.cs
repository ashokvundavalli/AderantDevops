using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text;
using System.Threading;
using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.Helpers {
    internal class PowerShellHelper {

        public static string RunCommand(string command, TestContext context, string directory = null) {
            if (directory == null) {
                directory = Path.Combine(context.DeploymentDirectory, "[" + DateTime.UtcNow.ToFileTimeUtc() + "]");
            }

            if (!Path.IsPathRooted(directory)) {
                throw new ArgumentException($"Path {directory} is not a rooted path.");
            }

            StringBuilder sb = new StringBuilder();

            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            sb.AppendLine("$InformationPreference = 'Continue'");
            sb.AppendLine("$DebugPreference = 'Continue'");
            sb.AppendLine("$ErrorActionPreference = 'Stop'");

            sb.AppendLine("Set-Location -LiteralPath " + directory.SurroundWith("'") + " -Verbose");
            sb.AppendLine("'Current script directory: ' + (Get-Location)");

            AssertCurrentDirectory();

            sb.AppendLine(command);

            var executor = new PowerShellPipelineExecutor();

            EventHandler<ICollection<PSObject>> dataReady = (sender, objects) => {
                foreach (var psObject in objects) {
                    context.WriteLine(MakeSafeForWriteLine(psObject));
                }
            };

            EventHandler<ICollection<object>> errorReady = (sender, objects) => {
                foreach (var psObject in objects) {
                    context.WriteLine(MakeSafeForWriteLine(psObject));
                }
            };

            EventHandler<InformationRecord> info = (sender, objects) => { context.WriteLine(objects.ToString()); };
            EventHandler<VerboseRecord> verbose = (sender, objects) => { context.WriteLine(objects.ToString()); };
            EventHandler<WarningRecord> warning = (sender, objects) => { context.WriteLine(objects.ToString()); };
            EventHandler<DebugRecord> debug = (sender, objects) => { context.WriteLine(objects.ToString()); };

            executor.DataReady += dataReady;
            executor.ErrorReady += errorReady;
            executor.Info += info;
            executor.Verbose += verbose;
            executor.Warning += warning;
            executor.Debug += debug;

            var cmd = new PSCommand();
            cmd.AddScript(sb.ToString());

            executor.RunScript(cmd,  null, CancellationToken.None);

            executor.DataReady -= dataReady;
            executor.ErrorReady -= errorReady;
            executor.Info -= info;
            executor.Verbose -= verbose;
            executor.Warning -= warning;
            executor.Debug -= debug;

            return directory;
        }

        internal static void AssertCurrentDirectory() {
            string currentDirectory = Environment.CurrentDirectory;
            if (currentDirectory.StartsWith("C:\\Program Files(x86)\\Microsoft Visual Studio", StringComparison.OrdinalIgnoreCase)) {
                throw new Exception($"Current directory is {currentDirectory} which is not allowed");
            }
        }

        private static string MakeSafeForWriteLine(object psObject) {
            return psObject.ToString().Replace("{", "{{").Replace("}", "}}");
        }
    }
}