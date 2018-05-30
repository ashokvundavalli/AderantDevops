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
        internal Context Context { get; set; }

        protected ContextTaskBase() {
            // Initialize context here for testing purposes.
            //Context = new Context {
            //    BuildRoot = new DirectoryInfo(@"C:\Git\ExpertSuite\Billing"),
            //    IsDesktopBuild = true,
            //    ComboBuildType = ComboBuildType.All
            //};
        }

        public override bool Execute() {
            Context context = ObtainContext();
            return ExecuteTask(context);
        }

        /// <summary>
        /// Obtains the ambient context from the build host.
        /// </summary>
        /// <returns></returns>
        protected virtual Context ObtainContext() {
            if (Context != null) {
                return Context;
            }

            object cachedContext = BuildEngine4.GetRegisteredTaskObject("BuildContext", Microsoft.Build.Framework.RegisteredTaskObjectLifetime.Build);
            Context = cachedContext as Context;
            if (Context != null) {
                Log.LogMessage(MessageImportance.Low, "Obtained context from registered task object");
                return Context;
            }

            Context = GetContextFromFile();
            Log.LogMessage(MessageImportance.Low, "Obtained context from registered memory mapped file");

            BuildEngine4.RegisterTaskObject("BuildContext", Context, Microsoft.Build.Framework.RegisteredTaskObjectLifetime.Build, false);

            return Context;
        }

        private Context GetContextFromFile() {
            Log.LogMessage(MessageImportance.Low, "Obtaining context from registered memory mapped file");
            string channelId = Environment.GetEnvironmentVariable(Constants.ContextChannelVariable);
            object contextObject = MemoryMappedBufferReaderWriter.Read(channelId, TimeSpan.FromMilliseconds(1000));
            Context = (Context)contextObject;

            return Context;
        }

        /// <summary>
        /// Implement this function to do your work.
        /// </summary>
        protected abstract bool ExecuteTask(Context context);
    }
}
