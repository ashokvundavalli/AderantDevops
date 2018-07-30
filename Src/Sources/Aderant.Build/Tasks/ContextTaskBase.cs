using System;
using System.Diagnostics;
using System.Threading;
using Aderant.Build.Ipc;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Provides the ultimate base class for context aware tasks within the build engine
    /// </summary>
    public abstract class ContextTaskBase : Task {
        private Context context;

        public virtual string ContextFileName { get; set; }

        public bool WaitForDebugger { get; set; }
 
        protected Context Context {
            get {
                return context ?? (context = ObtainContext());
            }
        }

        protected void ReplaceContext() {
            Register(Context);
            MemoryMappedFileReaderWriter.WriteData(ContextFileName, context);
        }

        public override bool Execute() {
            if (WaitForDebugger) {
                bool sleep = false;
                SpinWait.SpinUntil(() => {
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
                }, TimeSpan.FromMinutes(1));
            }

            context = ObtainContext();

            Debug.Assert(context != null);

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Obtains the ambient context from the build host.
        /// </summary>
        /// <returns></returns>
        protected virtual Context ObtainContext() {
            Context ctx;

            var cachedContext = BuildEngine4.GetRegisteredTaskObject("BuildContext", RegisteredTaskObjectLifetime.Build);
            ctx = cachedContext as Context;
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

        private void Register(Context ctx) {
            BuildEngine4.UnregisterTaskObject("BuildContext", RegisteredTaskObjectLifetime.Build);
            BuildEngine4.RegisterTaskObject("BuildContext", ctx, RegisteredTaskObjectLifetime.Build, false);
        }

        private Context GetContextFromFile() {
            if (string.IsNullOrEmpty(ContextFileName)) {
                ContextFileName = Environment.GetEnvironmentVariable(WellKnownProperties.ContextFileName);
            }

            Log.LogMessage("Retrieving context from file: {0}", ContextFileName);

            ErrorUtilities.IsNotNull(ContextFileName, nameof(ContextFileName));

            Context ctx;
            object contextObject = MemoryMappedFileReaderWriter.Read(ContextFileName);

            ctx = (Context)contextObject;
            return ctx;
        }
    }
}
