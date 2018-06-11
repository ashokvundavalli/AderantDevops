using System;
using Aderant.Build.Ipc;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Provides the ultimate base class for context aware tasks within the build engine
    /// </summary>
    public abstract class ContextTaskBase : Task {     

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
            Context context;

            var cachedContext = BuildEngine4.GetRegisteredTaskObject("BuildContext", Microsoft.Build.Framework.RegisteredTaskObjectLifetime.Build);
            context = cachedContext as Context;
            if (context != null) {
                return context;
            }

            context = GetContextFromFile();

            BuildEngine4.RegisterTaskObject("BuildContext", context, Microsoft.Build.Framework.RegisteredTaskObjectLifetime.Build, false);

            return context;
        }

        private Context GetContextFromFile() {
            Context context;
            var channelId = Environment.GetEnvironmentVariable(Constants.ContextChannelVariable);
            object contextObject = MemoryMappedBufferReaderWriter.Read(channelId);

            context = (Context)contextObject;
            return context;
        }

        /// <summary>
        /// Implement this function to do your work.
        /// </summary>
        protected abstract bool ExecuteTask(Context context);
    }
}