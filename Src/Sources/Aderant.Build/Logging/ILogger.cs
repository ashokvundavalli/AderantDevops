namespace Aderant.Build.Logging {
    /// <summary>
    /// Represts a common logging interface for MSBuild, PowerShell and another tools involved in the build toolkit.
    /// </summary>
    public interface ILogger {
        /// <summary>
        /// Writes a debug message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        void Debug(string message, params string[] args);

        /// <summary>
        /// Writes a message to the log.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        void Info(string message, params string[] args);

        /// <summary>
        /// Writes a warning message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        void Warning(string message, params string[] args);

        /// <summary>
        /// Writes an error message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        void Error(string message, params string[] args);
    }
}