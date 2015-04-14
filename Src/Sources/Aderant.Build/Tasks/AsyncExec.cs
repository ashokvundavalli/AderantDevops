using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class AsyncExec : Task {
        private bool started;

        [Required]
        public string Command { get; set; }
        
        public string WorkingDirectory { get; set; }

        public string LogFile { get; set; }

        public override bool Execute() {
            AutoResetEvent waitEvent = new AutoResetEvent(false);
            started = false;

            ThreadPool.QueueUserWorkItem(o => StartProcess(waitEvent));

            waitEvent.WaitOne();

            if (!started) {
                Log.LogError("AsyncExec - Process failed to start.");

                if (stringBuilder != null) {
                    Log.LogError(stringBuilder.ToString());
                }

                return false;
            }
            
            Log.LogMessage(MessageImportance.Low, "AsyncExec - Starting process..");
            return true;
        }

        private void StartProcess(AutoResetEvent waitEvent) {
            stringBuilder = new StringBuilder();
            string workingDirectory = null;

            using (var process = new System.Diagnostics.Process()) {
                process.StartInfo = GetProcessStartInfo(WorkingDirectory, Command);

                if (process.StartInfo != null) {
                    workingDirectory = process.StartInfo.WorkingDirectory;

                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;

                    process.ErrorDataReceived += (sender, args) => {
                        if (args != null) {
                            WriteToLogStream(args.Data);
                        }
                    };

                    process.OutputDataReceived += (sender, args) => {
                        if (args != null) {
                            WriteToLogStream(args.Data);
                        }
                    };
                }

                started = process.Start();
                waitEvent.Set();

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();
            }

            WriteLogFile(workingDirectory);
        }

        private void WriteLogFile(string workingDirectory) {
            string logFile = null;

            if (!string.IsNullOrEmpty(LogFile)) {
                logFile = LogFile;
            } else {
                if (workingDirectory != null) {
                    logFile = Path.Combine(workingDirectory, "AsyncExec.log");
                }
            }

            if (logFile != null) {
                string directoryName = Path.GetDirectoryName(logFile);
                if (directoryName != null) {
                    Directory.CreateDirectory(directoryName);
                }

                File.WriteAllText(logFile, stringBuilder.ToString());
            }
        }

        private StringBuilder stringBuilder;
        private object writeLock = new object();

        private void WriteToLogStream(string data) {
            if (!string.IsNullOrEmpty(data)) {
                lock (writeLock) {
                    stringBuilder.AppendLine(data);
                }
            }
        }

        internal string GetToolName() {
            var command =  Command.Split(' ')[0];

            command = command.Replace("\\",  string.Empty);

            return command.Replace("\"", string.Empty);
        }

        internal static string GetCommandArguments(string command) {
            Regex regex = new Regex(@"(?imnx-s:^((\""[^\""]+\"")|([^\ ]+))(?<Arguments>.*))");
            if (regex.IsMatch(command)) {
                string result = regex.Match(command).Groups["Arguments"].Value.Trim();
                return result;
            }

            return null;
        }

        protected virtual ProcessStartInfo GetProcessStartInfo(string workingDirectory, string command) {
            var arguments = GetCommandArguments(command);

            if (arguments.Length > 0x7d00)
                Log.LogWarningWithCodeFromResources("ToolTask.CommandTooLong", new object[] { GetType().Name });

            var startInfo = new ProcessStartInfo(GetToolName(), arguments) {
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false,
                UseShellExecute = false
            };

            if (workingDirectory != null) {
                startInfo.WorkingDirectory = workingDirectory;
            } else {
                startInfo.WorkingDirectory = Directory.GetCurrentDirectory();
            }

            return startInfo;
        }
    }
}