using System;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Provides the ultimate base class for context aware tasks within the build engine
    /// </summary>
    public abstract class BuildOperationContextTask : Task {
        private BuildOperationContext context;
        private bool executingTask;
        private BuildTaskLogger logger;
        private IBuildPipelineService pipelineService;

        public virtual string ContextEndpoint { get; set; }

        protected BuildOperationContext Context {
            get {
                if (InternalContext != null) {
                    return InternalContext;
                }

                return context ?? (context = ObtainContext());
            }
        }

        protected ILogger Logger {
            get { return logger ?? (logger = new BuildTaskLogger(Log)); }
        }

        internal static BuildOperationContext InternalContext { get; set; }

        internal IBuildPipelineService PipelineService {
            get { return Service ?? (Service = BuildPipelineServiceClient.CreateFromPipeId(ContextEndpoint ?? BuildPipelineServiceHost.PipeId)); }
        }

        internal IBuildPipelineService Service {
            get { return pipelineService; }
            set { pipelineService = value; }
        }

        public sealed override bool Execute() {
            if (executingTask) {
                return false;
            }

            executingTask = true;

            try {
                return ExecuteTask();
            } catch (Exception ex) {
                Exception exceptionToLog = ex;

                AggregateException aggregateException = ex as AggregateException;
                if (aggregateException != null) {
                    exceptionToLog = aggregateException.Flatten().InnerException;
                }

                Log.LogErrorFromException(exceptionToLog, true);
                return false;
            } finally {
                executingTask = false;

                if (Service != null) {
                    Service.Dispose();
                }
            }
        }

        public abstract bool ExecuteTask();

        /// <summary>
        /// Obtains the ambient context from the build host.
        /// </summary>
        protected virtual BuildOperationContext ObtainContext() {
            var ctx = PipelineService.GetContext();
            return ctx;
        }
    }
}
