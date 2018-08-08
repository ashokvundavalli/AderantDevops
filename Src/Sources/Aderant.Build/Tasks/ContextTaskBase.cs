﻿using System;
using System.Diagnostics;
using System.Management.Automation.Internal;
using System.Threading;
using Aderant.Build.Ipc;
using Aderant.Build.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Provides the ultimate base class for context aware tasks within the build engine
    /// </summary>
    public abstract class BuildOperationContextTask : Task {
        private BuildOperationContext context;
        private BuildTaskLogger logger;

        public virtual string ContextFileName { get; set; }

        public bool WaitForDebugger { get; set; }
 
        protected BuildOperationContext Context {
            get {
                if (InternalContext != null) {
                    return InternalContext;
                }
                return context ?? (context = ObtainContext());
            }
        }

        protected Aderant.Build.Logging.ILogger Logger {
            get {
                return logger ?? (logger = new BuildTaskLogger(this.Log));
            }
        }

        internal static BuildOperationContext InternalContext { get; set; }

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
        protected virtual BuildOperationContext ObtainContext() {
            BuildOperationContext ctx;

            var cachedContext = BuildEngine4.GetRegisteredTaskObject("BuildContext", RegisteredTaskObjectLifetime.Build);
            ctx = cachedContext as BuildOperationContext;
            if (ctx != null) {
                Log.LogMessage("Retrieved context from registered task object storage");
                return ctx;
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

            Log.LogMessage("Retrieving context from file: {0}", ContextFileName);

            ErrorUtilities.IsNotNull(ContextFileName, nameof(ContextFileName));

            BuildOperationContext ctx;
            object contextObject = MemoryMappedFileReaderWriter.Read(ContextFileName);

            ctx = (BuildOperationContext)contextObject;
            return ctx;
        }
    }

    internal class InternalTestHost {

        public BuildOperationContext Context { get; set; }
    }
}
