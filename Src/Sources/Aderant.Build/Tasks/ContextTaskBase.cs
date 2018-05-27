using System;
using Aderant.Build.Ipc;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Provides the ultimate base class for context aware tasks within the build engine
    /// </summary>
    public abstract class ContextTaskBase : Task {
        private string channelId;

        public ContextTaskBase() {
        }

        public override bool Execute() {
            var context = ObtainContext();
            return ExecuteTask(context);
        }

        /// <summary>
        /// Obtains the ambient context from the build host.
        /// </summary>
        /// <returns></returns>
        protected virtual Context ObtainContext() {
            channelId = Environment.GetEnvironmentVariable("BuildContextChannelId");

            object contextObject = MemoryMappedBufferReaderWriter.Read(channelId);

            return (Context)contextObject;
        }

        /// <summary>
        /// Implement this function to do your work.
        /// </summary>
        protected abstract bool ExecuteTask(Context context);
    }
}