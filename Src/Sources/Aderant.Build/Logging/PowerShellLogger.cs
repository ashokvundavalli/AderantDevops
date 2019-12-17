using System.Globalization;
using System.Management.Automation.Host;

namespace Aderant.Build.Logging {
    /// <summary>
    /// A PowerShell implementation of the <see cref="ILogger"/> interface.
    /// Allows internal components to write to a PowerShell host.
    /// </summary>
    public class PowerShellLogger : ILogger {
        private PSHostUserInterface host;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellLogger"/> class.
        /// </summary>
        /// <param name="host">The host.</param>
        public PowerShellLogger(PSHost host) : this(host.UI) {

        }

        public PowerShellLogger(PSHostUserInterface userInterface) {
            this.host = userInterface;
        }

        /// <summary>
        /// Writes a debug message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The args.</param>
        public void Debug(string message, params object[] args) {
            host.WriteDebugLine(FormatMessage(message, args));
        }

        /// <summary>
        /// Writes a message to the log.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The args.</param>
        public void Info(string message, params object[] args) {
            host.WriteLine(FormatMessage(message, args));
        }

        /// <summary>
        /// Writes a warning message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The args.</param>
        public void Warning(string message, params object[] args) {
            host.WriteWarningLine(FormatMessage(message, args));
        }

        /// <summary>
        /// Writes an error message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void Error(string message, params object[] args) {
            host.WriteErrorLine(FormatMessage(message, args));
        }

        private static string FormatMessage(string message, object[] args) {
            if (args != null && args.Length > 0) {
                message = string.Format(CultureInfo.CurrentCulture, message, args);
            }
            return message;
        }
    }
}
