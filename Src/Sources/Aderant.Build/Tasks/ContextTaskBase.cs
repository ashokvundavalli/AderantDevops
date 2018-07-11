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
                Log.LogMessage("Retrieved context from registered task object");
                return ctx;
            }

            Log.LogMessage("Retrieving context from file");

            ctx = GetContextFromFile();

            if (ctx != null) {
                BuildEngine4.RegisterTaskObject("BuildContext", ctx, RegisteredTaskObjectLifetime.Build, false);
            }

            return ctx;
        }

        private Context GetContextFromFile() {
            if (string.IsNullOrEmpty(ContextFileName)) {
                ContextFileName = Environment.GetEnvironmentVariable(WellKnownProperties.ContextFileName);
            }

            Context ctx;
            object contextObject = MemoryMappedFileReaderWriter.Read(ContextFileName);

            ctx = (Context)contextObject;
            return ctx;
        }
    }
}
