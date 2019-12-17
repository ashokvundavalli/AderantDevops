using System.Linq;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class WriteBuildStateFile : BuildOperationContextTask {

        /// <summary>
        /// The state files produced during this build
        /// </summary>
        [Output]
        public string[] WrittenStateFiles { get; private set; }

        public override bool ExecuteTask() {
            var writer = new BuildStateWriter(Logger);
            writer.WriteStateFiles(PipelineService, Context);

            WrittenStateFiles = writer.WrittenStateFiles.ToArray();

            return !Log.HasLoggedErrors;
        }
    }

}
