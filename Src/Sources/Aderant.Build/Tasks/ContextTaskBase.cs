using System;
using System.Diagnostics;
using System.Threading;
using Aderant.Build.Ipc;
using Aderant.Build.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ILogger = Aderant.Build.Logging.ILogger;

namespace Aderant.Build.Tasks {

    public class WaitForDebugger : Task {

        public bool Wait { get; set; }

        public override bool Execute() {
            if (Environment.UserInteractive) {
                if (Wait) {
                    bool sleep = false;
                    SpinWait.SpinUntil(
                        () => {
                            if (sleep) {
                                Thread.Sleep(TimeSpan.FromMilliseconds(500));
                            }

                            Log.LogMessage("Waiting for debugger... [C] to cancel waiting");
                            if (Console.KeyAvailable) {
                                var consoleKeyInfo = Console.ReadKey(true);
                                if (consoleKeyInfo.Key == ConsoleKey.C) {
                                    return true;
                                }
                            }

                            sleep = true;
                            return Debugger.IsAttached;
                        },
                        TimeSpan.FromSeconds(10));
                }
            }

            return !Log.HasLoggedErrors;
        }
    }

    /// <summary>
    /// Provides the ultimate base class for context aware tasks within the build engine
    /// </summary>
    public abstract class BuildOperationContextTask : Task {
        private BuildOperationContext context;
        private bool executingTask;
        private BuildTaskLogger logger;

        public virtual string ContextFileName { get; set; }

        protected BuildOperationContext Context {
            get {
                if (InternalContext != null) {
                    return InternalContext;
                }

                return context ?? (context = ObtainContext());
            }
        }

        protected ILogger Logger {
            get { return logger ?? (logger = new BuildTaskLogger(this.Log)); }
        }

        internal static BuildOperationContext InternalContext { get; set; }
        internal IBuildCommunicator ContextService { get; set; }

        public sealed override bool Execute() {
            if (executingTask) {
                return false;
            }

            executingTask = true;

            try {
                return ExecuteTask();
            } finally {
                executingTask = false;
            }
        }

        public abstract bool ExecuteTask();

        /// <summary>
        /// Obtains the ambient context from the build host.
        /// </summary>
        protected virtual BuildOperationContext ObtainContext() {
            BuildOperationContext ctx;

            var cachedContext = BuildEngine4.GetRegisteredTaskObject("BuildContext", RegisteredTaskObjectLifetime.Build);
            ctx = cachedContext as BuildOperationContext;
            if (ctx != null) {
                //Log.LogMessage("Retrieved context from registered task object storage");
                //return ctx;
            }

            ctx = GetContextFromFile();

            if (ctx != null) {
                Register(ctx);
            }

            return ctx;
        }

        private void Register(BuildOperationContext ctx) {
            BuildEngine4.UnregisterTaskObject("BuildContext", RegisteredTaskObjectLifetime.Build);
            BuildEngine4.RegisterTaskObject("BuildContext", ctx, RegisteredTaskObjectLifetime.Build, false);
        }

        private BuildOperationContext GetContextFromFile() {
            if (string.IsNullOrEmpty(ContextFileName)) {
                ContextFileName = Environment.GetEnvironmentVariable(WellKnownProperties.ContextFileName);
            }

            //Log.LogMessage("Retrieving context from file: {0}", ContextFileName);

            //ErrorUtilities.IsNotNull(ContextFileName, nameof(ContextFileName));

            //BuildOperationContext ctx;
            //object contextObject = MemoryMappedFileReaderWriter.Read(ContextFileName);

            //ctx = (BuildOperationContext)contextObject;
            //return ctx;
            ContextService = BuildContextService.CreateProxy(ContextFileName);
            return ContextService.GetContext();
        }
    }
}
