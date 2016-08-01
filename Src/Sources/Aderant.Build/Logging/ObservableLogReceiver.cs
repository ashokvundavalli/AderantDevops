using System;
using System.Diagnostics;
using Microsoft.FSharp.Core;

namespace Aderant.Build.Logging {
    internal class ObservableLogReceiver : FSharpFunc<Paket.Logging.Trace, Unit> {
        private readonly ILogger logger;

        public ObservableLogReceiver(ILogger logger) {
            this.logger = logger;
        }

        public override Unit Invoke(Paket.Logging.Trace trace) {
            if (logger == null) {
                return null;
            }

            if (trace.Level == TraceLevel.Verbose) {
                logger.Debug(trace.Text);
            }

            if (trace.Level == TraceLevel.Info) {
                logger.Info(trace.Text);
            }

            if (trace.Level == TraceLevel.Warning) {
                logger.Warning(trace.Text);
            }

            if (trace.Level == TraceLevel.Error) {
                logger.Error(trace.Text);
            }

            return null;
        }
    }
}