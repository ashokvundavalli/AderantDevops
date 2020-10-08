using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Aderant.Build.Logging {
    /// <summary>
    /// A PowerShell implementation of the <see cref="ILogger" /> interface.
    /// Allows internal components to write to a PowerShell host.
    /// </summary>
    public class PowerShellLogger : ILogger {
        private readonly PSCmdlet cmdlet;

        private const int DebugLevel = 0;
        private const int InfoLevel = 1;
        private const int WarningLevel = 2;
        private const int ErrorLevel = 3;

        private static ConditionalWeakTable<PSHostUserInterface, ILogger> loggerTable = new ConditionalWeakTable<PSHostUserInterface, ILogger>();
        private static object syncLock = new object();
        private ConcurrentQueue<Tuple<int, string, object[]>> pendingWrites = new ConcurrentQueue<Tuple<int, string, object[]>>();
        private Thread permittedToWriteThread;
        private Action<string> writeDebug;
        private Action<ErrorRecord> writeError;

        private Action<object, string[]> writeInformation;
        private Action<string> writeWarning;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellLogger" /> class.
        /// </summary>
        public PowerShellLogger(PSCmdlet cmdlet) {
            this.cmdlet = cmdlet;
            permittedToWriteThread = Thread.CurrentThread;

            writeInformation = cmdlet.WriteInformation;
            writeDebug = cmdlet.WriteDebug;
            writeWarning = cmdlet.WriteWarning;
            writeError = cmdlet.WriteError;
        }

        /// <summary>
        /// Writes a debug message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The args.</param>
        public void Debug(string message, params object[] args) {
            CheckPermittedToWrite(DebugLevel, message, args);
        }

        /// <summary>
        /// Writes a message to the log.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The args.</param>
        public void Info(string message, params object[] args) {
            CheckPermittedToWrite(InfoLevel, message, args);
        }

        /// <summary>
        /// Writes a warning message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The args.</param>
        public void Warning(string message, params object[] args) {
            CheckPermittedToWrite(WarningLevel, message, args);
        }

        /// <summary>
        /// Writes an error message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void Error(string message, params object[] args) {
            CheckPermittedToWrite(ErrorLevel, message, args);
        }

        private void CheckPermittedToWrite(int level, string message, object[] args) {
            // If there is no command runtime we cannot log
            if (cmdlet.CommandRuntime != null) {
                // PowerShell does not allow cross thread writes, if we are not on the cmdlet thread then defer the message
                if (Thread.CurrentThread != permittedToWriteThread) {
                    pendingWrites.Enqueue(Tuple.Create(level, message, args));
                } else {
                    WriteMessage(level, message, args);

                    if (pendingWrites.Count > 0) {
                        while (pendingWrites.Count > 0) {
                            Tuple<int, string, object[]> value;
                            if (pendingWrites.TryDequeue(out value)) {
                                WriteMessage(value.Item1, value.Item2, value.Item3);
                            }
                        }
                    }
                }
            }
        }

        private void WriteMessage(int level, string message, object[] args) {
            message = LoggerExtensions.FormatMessage(message, args);

            switch (level) {
                case DebugLevel: {
                    if (writeDebug != null) {
                        writeDebug(message);
                    }
                    break;
                    }
                case InfoLevel: {
                    if (writeInformation != null) {
                        writeInformation(message, null);
                    }
                    break;
                    }
                case WarningLevel: {
                    if (writeWarning != null) {
                        writeWarning(message);
                    }
                    break;
                    }
                case ErrorLevel: {
                        LogError(message);
                        break;
                    }
            }
        }

        private void LogError(string message) {
            var err =
                new ErrorRecord(
                    new Exception(message),
                    null,
                    ErrorCategory.WriteError,
                    null);
            err.ErrorDetails =
                new ErrorDetails(message);

            if (writeError != null) {
                writeError(err);
            }
        }

        public static ILogger Create(PSHost host) {
            return Create(host.UI);
        }

        public static ILogger Create(PSHostUserInterface userInterface) {
            if (userInterface == null) {
                return NullLogger.Default;
            }

            // Prevent duplicate subscriptions to the same host
            lock (syncLock) {
                ILogger logger;
                if (loggerTable.TryGetValue(userInterface, out logger)) {
                    return logger;
                }

                logger = loggerTable.GetValue(userInterface, key => new DirectPowerShellLogger(userInterface));
                return logger;
            }
        }
    }

    internal class DirectPowerShellLogger : ILogger {
        private readonly PSHostUserInterface host;

        public DirectPowerShellLogger(PSHostUserInterface userInterface) {
            host = userInterface;
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
            return LoggerExtensions.FormatMessage(message, args);
        }
    }
}
