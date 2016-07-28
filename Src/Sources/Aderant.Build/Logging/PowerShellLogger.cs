using System.Globalization;
using System.Management.Automation.Host;

namespace Aderant.Build.Logging {
    /// <summary>
    /// A PowerShell implementation of the <see cref="ILogger"/> interface. 
    /// Allows internal components to write to a PowerShell host.
    /// </summary>
    public class PowerShellLogger : ILogger {
        private PSHost host;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellLogger"/> class.
        /// </summary>
        /// <param name="host">The host.</param>
        public PowerShellLogger(PSHost host) {
            this.host = host;
        }

        /// <summary>
        /// Writes a debug message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The args.</param>
        public void Debug(string message, params string[] args) {
            host.UI.WriteDebugLine(FormatMessage(message, args));
        }

        /// <summary>
        /// Writes a message to the log.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The args.</param>
        public void Info(string message, params string[] args) {
            host.UI.WriteLine(FormatMessage(message, args));
        }

        /// <summary>
        /// Writes a warning message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The args.</param>
        public void Warning(string message, params string[] args) {
            host.UI.WriteWarningLine(FormatMessage(message, args));
        }

        /// <summary>
        /// Writes an error message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void Error(string message, params string[] args) {
            host.UI.WriteErrorLine(FormatMessage(message, args));
        }

        private static string FormatMessage(string message, string[] args) {
            if (args != null) {
                message = string.Format(CultureInfo.CurrentCulture, message, args);
            }
            return message;
        }
    }
}