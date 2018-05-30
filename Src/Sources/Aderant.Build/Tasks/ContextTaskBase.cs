using System;
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
            
            var cachedContext = BuildEngine4.GetRegisteredTaskObject("BuildContext", RegisteredTaskObjectLifetime.Build);
            context = cachedContext as Context;
            if (context != null) {
                Log.LogMessage(MessageImportance.Low, "Obtained context from registered task object");
                return context;
            }

            context = GetContextFromFile();
            Log.LogMessage(MessageImportance.Low, "Obtained context from registered memory mapped file");

            BuildEngine4.RegisterTaskObject("BuildContext", context, RegisteredTaskObjectLifetime.Build, false);

            return context;
        }

        private Context GetContextFromFile() {
            Log.LogMessage(MessageImportance.Low, "Obtaining context from registered memory mapped file");

            var channelId = Environment.GetEnvironmentVariable(Constants.ContextChannelVariable);
            object contextObject = MemoryMappedBufferReaderWriter.Read(channelId, TimeSpan.FromMilliseconds(1000));

            var context = (Context)contextObject;
            return context;
        }

        /// <summary>
        /// Implement this function to do your work.
        /// </summary>
        protected abstract bool ExecuteTask(Context context);
    }
}