using Aderant.Build.ProjectSystem.StateTracking;

namespace Aderant.Build.Tasks {
    public class WriteBuildStateFile : BuildOperationContextTask {

        public override bool ExecuteTask() {
            var writer = new BuildStateWriter();
            writer.WriteStateFiles(Context);

            PipelineService.Publish(Context);

            return !Log.HasLoggedErrors;
        }
    }
}
