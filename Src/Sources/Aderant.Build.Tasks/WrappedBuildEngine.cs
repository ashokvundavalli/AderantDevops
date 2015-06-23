using System;
using System.Collections;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    internal class WrappedBuildEngine : IBuildEngine {
        private readonly IBuildEngine buildEngine;

        public WrappedBuildEngine(IBuildEngine buildEngine) {
            this.buildEngine = buildEngine;
        }

        public void LogErrorEvent(BuildErrorEventArgs e) {
            
            if (SuppressSrcToolExitCode) {
                if (string.Equals(e.Code, "MSB6006")) {
                    if (e.Message.IndexOf("\"srctool.exe\" exited with code -1.", StringComparison.OrdinalIgnoreCase) >= 0) {
                        LogMessageEvent(new BuildMessageEventArgs(e.Subcategory,
                            e.Code,
                            e.File,
                            e.LineNumber,
                            e.ColumnNumber,
                            e.EndLineNumber,
                            e.ColumnNumber,
                            e.Message + " This error was converted to a message.",
                            e.HelpKeyword,
                            e.SenderName,
                            MessageImportance.Normal,
                            e.Timestamp));
                        return;
                    }
                }
            }

            buildEngine.LogErrorEvent(e);
        }

        public void LogWarningEvent(BuildWarningEventArgs e) {
            buildEngine.LogWarningEvent(e);
        }

        public void LogMessageEvent(BuildMessageEventArgs e) {
            buildEngine.LogMessageEvent(e);
        }

        public void LogCustomEvent(CustomBuildEventArgs e) {
            buildEngine.LogCustomEvent(e);
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) {
            return buildEngine.BuildProjectFile(projectFileName, targetNames, globalProperties, targetOutputs);
        }

        public bool ContinueOnError {
            get { return buildEngine.ContinueOnError; }
        }

        public int LineNumberOfTaskNode {
            get { return buildEngine.LineNumberOfTaskNode; }
        }

        public int ColumnNumberOfTaskNode {
            get { return buildEngine.ColumnNumberOfTaskNode; }
        }

        public string ProjectFileOfTaskNode {
            get { return buildEngine.ProjectFileOfTaskNode; }
        }

        public bool SuppressSrcToolExitCode { get; set; }
    }
}