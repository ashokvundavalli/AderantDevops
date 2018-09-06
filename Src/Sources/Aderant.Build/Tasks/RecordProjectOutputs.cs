using System;
using Aderant.Build.ProjectSystem.StateTracking;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Records outputs from an MSBuild project.
    /// Used to drive the the build cache resolution algorithm
    /// </summary>
    public class RecordProjectOutputs : BuildOperationContextTask {

        [Required]
        public string ProjectFile { get; set; }

        [Required]
        public string IntermediateDirectory { get; set; }

        [Required]
        public ITaskItem OutputPath { get; set; }

        [Required]
        public string ProjectGuid { get; set; }

        public string[] ProjectOutputs { get; set; }

        public string[] ProjectTypeGuids { get; set; }

        public string TestProjectType { get; set; }

        public string[] References { get; set; }

        public override bool ExecuteTask() {

            var builder = new ProjectOutputSnapshotBuilder {
                SourcesDirectory = Context.BuildMetadata.BuildSourcesDirectory,
                ProjectFile = ProjectFile,
                ProjectOutputs = ProjectOutputs,
                OutputPath = OutputPath.ItemSpec,
                IntermediateDirectory = IntermediateDirectory,
                ProjectTypeGuids = ProjectTypeGuids,
                TestProjectType = TestProjectType,
                References = References,
            };

            var snapshot = builder.BuildSnapshot(Guid.Parse(ProjectGuid));

            PipelineService.RecordProjectOutputs(snapshot);

            return !Log.HasLoggedErrors;
        }
    }
}
