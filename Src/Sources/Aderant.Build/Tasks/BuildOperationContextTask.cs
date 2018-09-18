﻿using System;
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
            get { return pipelineService ?? (pipelineService = BuildPipelineServiceClient.CreateFromPipeId(ContextEndpoint ?? BuildPipelineServiceHost.PipeId)); }
        }

        public sealed override bool Execute() {
            if (executingTask) {
                return false;
            }

            executingTask = true;

            try {
                return ExecuteTask();
            } catch (Exception ex) {
                Log.LogErrorFromException(ex);
                return false;
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
            var ctx = PipelineService.GetContext();
            return ctx;
        }
    }
}
