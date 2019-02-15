using Aderant.Build.AzurePipelines;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Logging {
    public class BuildTaskLogger : ILogger {
        private bool? runningOnAzurePipelines;

        public BuildTaskLogger(Task hostTask)
            : this(hostTask.Log) {
        }

        public BuildTaskLogger(TaskLoggingHelper helper) {
            this.Logger = helper;
        }

        internal TaskLoggingHelper Logger { get; private set; }

        /// <summary>
        /// Writes a debug message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void Debug(string message, params object[] args) {
            Logger.LogMessage(MessageImportance.Low, message, args);
        }

        /// <summary>
        /// Writes a message to the log.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void Info(string message, params object[] args) {
            Logger.LogMessage(MessageImportance.Normal, message, args);
        }

        /// <summary>
        /// Writes a warning message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void Warning(string message, params object[] args) {
            Logger.LogWarning(FormatWarningForAzurePipelines(message), args);
        }

        /// <summary>
        /// Writes an error message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void Error(string message, params object[] args) {
            Logger.LogError(FormatErrorForAzurePipelines(message), args);
        }

        private string FormatWarningForAzurePipelines(string message) {
            if (runningOnAzurePipelines == null) {
                runningOnAzurePipelines = VsoCommandBuilder.IsAzurePipelines;
            }

            if (runningOnAzurePipelines.GetValueOrDefault()) {
                return VsoCommandBuilder.FormatWarning(message);
            }

            return message;
        }

        private string FormatErrorForAzurePipelines(string message) {
            if (runningOnAzurePipelines == null) {
                runningOnAzurePipelines = VsoCommandBuilder.IsAzurePipelines;
            }

            if (runningOnAzurePipelines.GetValueOrDefault()) {
                return VsoCommandBuilder.FormatError(message);
            }

            return message;
        }
    }
}