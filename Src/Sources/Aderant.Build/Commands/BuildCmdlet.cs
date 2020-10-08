using System;
using System.Management.Automation;
using System.Text;
using System.Threading;
using Aderant.Build.Logging;

namespace Aderant.Build.Commands {
    public abstract class BuildCmdlet : PSCmdlet {
        private ILogger logger;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        protected sealed override void ProcessRecord() {
            try {
                Process();
            } catch (AggregateException exception) {
                foreach (var ex in exception.InnerExceptions) {
                    this.Logger.Error(FormatException(ex));
                }
                throw;
            } catch (Exception ex) {
                this.Logger.Error(FormatException(ex));
                throw;
            }
        }

        protected override void StopProcessing() {
            cancellationTokenSource.Cancel();

            base.StopProcessing();
        }

        /// <summary>
        /// Gets or sets a PowerShell aware logger.
        /// </summary>
        public ILogger Logger {
            get {
                if (logger == null) {
                    logger = new PowerShellLogger(this);
                }
                return logger;
            }

            set { logger = value; }
        }

        /// <summary>
        /// Gives you a cancellation token connected to the PowerShell pipeline to abort operations.
        /// </summary>
        protected CancellationToken CancellationToken {
            get {
                return cancellationTokenSource.Token;
            }
        }

        /// <summary>
        /// Formats an exception to be placed in the debug output.
        /// </summary>
        /// <param name="ex">
        /// The exception.
        /// </param>
        /// <returns>
        /// A string that represents the message to display for the exception.
        /// </returns>
        protected string FormatException(Exception ex) {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine(ex.Message);
            builder.AppendLine(ex.StackTrace);

            var aggex = ex as AggregateException;
            if (aggex != null) {
                foreach (var innerException in aggex.InnerExceptions) {
                    builder.AppendLine(this.FormatException(innerException));
                }
            } else if (ex.InnerException != null) {
                builder.AppendLine(this.FormatException(ex.InnerException));
            }

            return builder.ToString();
        }

        protected abstract void Process();
    }
}
