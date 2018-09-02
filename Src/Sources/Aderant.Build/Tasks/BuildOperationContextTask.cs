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
        private IBuildPipelineServiceContract pipelineService;

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
            get { return logger ?? (logger = new BuildTaskLogger(Log)); }
        }

        internal static BuildOperationContext InternalContext { get; set; }

        internal IBuildPipelineServiceContract PipelineService {
            get { return pipelineService ?? (pipelineService = BuildPipelineServiceFactory.Instance.GetProxy(ContextFileName)); }
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

                if (pipelineService != null) {
                    pipelineService.Dispose();
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
            return PipelineService.GetContext();
        }
    }
}
