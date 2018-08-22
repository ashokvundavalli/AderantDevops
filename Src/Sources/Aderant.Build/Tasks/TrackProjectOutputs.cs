using System;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Records outputs from an MSBuild project.
    /// Used to drive the the build cache resolution algorithm
    /// </summary>
    public class TrackProjectOutputs : BuildOperationContextTask {

        [Required]
        public string ProjectFile { get; set; }

        [Required]
        public string IntermediateDirectory { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Required]
        public string ProjectGuid { get; set; }

        public string[] ProjectOutputs { get; set; }

        public string[] ProjectTypeGuids { get; set; }

        public string TestProjectType { get; set; }

        public string[] References { get; set; }

        public override bool ExecuteTask() {

            var snapshot = Context.RecordProjectOutputs(
                Guid.Parse(ProjectGuid),
                Context.BuildMetadata.BuildSourcesDirectory,
                ProjectFile,
                ProjectOutputs,
                OutputPath,
                IntermediateDirectory,
                ProjectTypeGuids,
                TestProjectType,
                References);

            PipelineService.RecordProjectOutputs(snapshot);

            return !Log.HasLoggedErrors;
        }
    }
}
