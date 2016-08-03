using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Logging {
    internal class BuildTaskLogger : ILogger {
        internal TaskLoggingHelper Logger { get; private set; }

        internal BuildTaskLogger(Microsoft.Build.Utilities.Task hostTask) {
            Logger = hostTask.Log;
        }

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
            Logger.LogWarning(message, args);
        }

        /// <summary>
        /// Writes an error message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void Error(string message, params object[] args) {
            Logger.LogError(message, args);
        }
    }
}
