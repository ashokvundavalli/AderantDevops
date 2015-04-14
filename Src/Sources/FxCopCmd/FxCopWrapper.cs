﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace Aderant.Build {
    internal class FxCopWrapper {
        // Defined in Microsoft.Build.Tasks.CodeAnalysis as the exit code thrown when a indirect reference is missing.
        private const int AssemblyReferencesError = 512;

        private readonly string commandLine;
        private readonly object eventCloseLock = new object();
        private bool eventsDisposed;
        private int exitCode;
        private ManualResetEvent toolExited;

        public FxCopWrapper(string commandLine) {
            this.commandLine = commandLine;
        }

        protected virtual string ToolName {
            get { return "FxCopCmd.exe"; }
        }

        private static string CleanResponseFileText(string commandLine, Match match) {
            return commandLine.Replace(match.Groups[0].Value, string.Empty);
        }

        internal int Execute() {
            if (commandLine.Contains("CodeAnalysisOriginalPath")) {
                Match match = Regex.Match(commandLine, @"(CodeAnalysisOriginalPath=(.*)\\)");
                string capture = match.Groups[2].Value;

                if (!string.IsNullOrEmpty(capture) && Directory.Exists(capture)) {
                    string pathToTool = Path.Combine(capture, ToolName);

                    return ExecuteTool(pathToTool, null, CleanResponseFileText(commandLine, match));
                }
            }
            return -1;
        }

        internal int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands) {
            Process process = null;
            toolExited = new ManualResetEvent(false);
            eventsDisposed = false;

            try {
                process = new Process();
                process.StartInfo = GetProcessStartInfo(pathToTool, commandLineCommands);
                process.EnableRaisingEvents = true;

                process.Exited += (sender, e) => {
                    lock (eventCloseLock) {
                        if (!eventsDisposed) {
                            toolExited.Set();
                        }
                    }
                };
                process.ErrorDataReceived += (sender, e) => ReceiveStandardErrorOrOutputData(e, true);
                process.OutputDataReceived += (sender, e) => ReceiveStandardErrorOrOutputData(e, false);
                exitCode = -1;

                process.Start();

                process.StandardInput.Close();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();
                while (!process.HasExited) {
                    Thread.Sleep(50);
                }
            } finally {
                if (process != null) {
                    try {
                        exitCode = process.ExitCode;
                    } catch (InvalidOperationException) {
                    }
                    process.Close();
                    process.Dispose();
                    process = null;
                }

                if (exitCode == AssemblyReferencesError) {
                    string message = "FxCop completed with error " + AssemblyReferencesError + " - Missing indirect reference. This exit code will be ignored.";
                    string dashes = new string('-', message.Length);
                    Console.WriteLine(dashes);
                    Console.WriteLine(message);
                    Console.WriteLine(dashes);

                    exitCode = 0;
                }

                lock (eventCloseLock) {
                    eventsDisposed = true;
                    toolExited.Close();
                }
            }

            return exitCode;
        }

        private static ProcessStartInfo GetProcessStartInfo(string pathToTool, string commandLineCommands) {
            var processStartInfo = new ProcessStartInfo(pathToTool, commandLineCommands);

            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardOutput = true;

            processStartInfo.RedirectStandardInput = true;
            return processStartInfo;
        }

        private static void ReceiveStandardErrorOrOutputData(DataReceivedEventArgs e, bool isError) {
            if (e.Data != null) {
                Console.WriteLine(e.Data);

                if (isError) {
                    Console.Error.WriteLine(e.Data);
                }
            }
        }
    }
}