using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class TrackProjectOutputs : BuildOperationContextTask {

        [Required]
        public string ProjectFile { get; set; }

        [Required]
        public string IntermediateDirectory { get; set; }

        [Required]
        public string OutputPath { get; set; }

        public string[] ProjectOutputs { get; set; }

        public string[] ProjectTypeGuids { get; set; }

        public string TestProjectType { get; set; }

        protected override bool UpdateContextOnCompletion { get; set; } = true;

        public override bool ExecuteTask() {
            Context.RecordProjectOutputs(
                Context.BuildMetadata.BuildSourcesDirectory,
                ProjectFile,
                ProjectOutputs,
                OutputPath,
                IntermediateDirectory,
                ProjectTypeGuids,
                TestProjectType);
            return !Log.HasLoggedErrors;
        }
    }
}
