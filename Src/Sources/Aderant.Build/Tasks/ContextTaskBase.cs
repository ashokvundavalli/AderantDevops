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

        public ContextTaskBase() {
        }

        public virtual string ContextFileName { get; set; }

        public bool WaitForDebugger { get; set; }

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

            var context = ObtainContext();

            Debug.Assert(context != null);

            return ExecuteTask(context);
        }

        /// <summary>
        /// Obtains the ambient context from the build host.
        /// </summary>
        /// <returns></returns>
        protected virtual Context ObtainContext() {
            Context context;

            var cachedContext = BuildEngine4.GetRegisteredTaskObject("BuildContext", RegisteredTaskObjectLifetime.Build);
            context = cachedContext as Context;
            if (context != null) {
                Log.LogMessage("Retrieved context from registered task object");
                return context;
            }

            Log.LogMessage("Retrieving context from file");

            context = GetContextFromFile();

            if (context != null) {
                BuildEngine4.RegisterTaskObject("BuildContext", context, RegisteredTaskObjectLifetime.Build, false);
            }

            return context;
        }

        private Context GetContextFromFile() {
            if (string.IsNullOrEmpty(ContextFileName)) {
                ContextFileName = Environment.GetEnvironmentVariable(WellKnownProperties.ContextFileName);
            }

            Context context;
            object contextObject = MemoryMappedFileReaderWriter.Read(ContextFileName);

            context = (Context)contextObject;
            return context;
        }

        /// <summary>
        /// Implement this function to do your work.
        /// </summary>
        protected abstract bool ExecuteTask(Context context);
    }
}
