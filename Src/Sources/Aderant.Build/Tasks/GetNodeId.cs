using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Gets the current NodeId of MSBuild.exe in a multi-processor build.
    /// </summary>
    public class GetNodeId : Task {
        private static int nodeId = -1;

        [Output]
        public int NodeId {
            get { return nodeId; }
        }

        /// <summary>
        /// Execute the task.
        /// </summary>
        public override bool Execute() {
            if (nodeId == -1) {
                nodeId = GetNodeIdFromEngine(BuildEngine);
            }

            return true;
        }

        /// <summary>
        /// This code is a necessary evil. We must dive into private state to rip out the nodeId of the build worker.
        /// This code is likely to fail as MSBuild evolves.
        /// </summary>
        internal static int GetNodeIdFromEngine(IBuildEngine buildEngine) {
            PropertyInfo loggingContextProperty = buildEngine.GetType().GetProperty("LoggingContext", BindingFlags.Instance | BindingFlags.NonPublic);
            var loggingContextValue = loggingContextProperty.GetValue(buildEngine, null);

            PropertyInfo loggingServiceProperty = loggingContextValue.GetType().GetProperty("LoggingService", BindingFlags.Instance | BindingFlags.Public);
            object loggingServiceValue = loggingServiceProperty.GetValue(loggingContextValue);

            var fieldInfo = loggingServiceValue.GetType().GetField("_nodeId", BindingFlags.Instance | BindingFlags.NonPublic);
            return (int)fieldInfo.GetValue(loggingServiceValue);
        }

    }
}