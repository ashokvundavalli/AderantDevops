using System;
using Aderant.Build.Ipc;
using Aderant.Build.Logging;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Provides the ultimate base class for context aware tasks within the build engine
    /// </summary>
    public abstract class BuildOperationContextTask : Task {
        private BuildOperationContext context;
        private IContextServiceContract contextService;
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

        internal IContextServiceContract ContextService {
            get { return contextService ?? (contextService = ObtainContextService()); }
        }

        public sealed override bool Execute() {
            if (executingTask) {
                return false;
            }

            executingTask = true;

            try {
                return ExecuteTask();
            } finally {
                executingTask = false;

                if (contextService != null) {
                    contextService.Dispose();
                }
            }
        }

        public abstract bool ExecuteTask();

        /// <summary>
        /// Obtains the ambient context from the build host.
        /// </summary>
        protected virtual BuildOperationContext ObtainContext() {
            var ctx = GetContextFromFile();
            return ctx;
        }

        private BuildOperationContext GetContextFromFile() {
            return ContextService.GetContext();
        }

        private IContextServiceContract ObtainContextService() {
            if (string.IsNullOrEmpty(ContextFileName)) {
                ContextFileName = Environment.GetEnvironmentVariable(WellKnownProperties.ContextFileName);
            }

            ErrorUtilities.IsNotNull(ContextFileName, nameof(ContextFileName));

            return BuildContextService.CreateProxy(ContextFileName);
        }
    }
}
