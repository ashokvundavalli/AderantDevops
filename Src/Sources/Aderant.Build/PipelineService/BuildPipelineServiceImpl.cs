using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Aderant.Build.Packaging;

namespace Aderant.Build.PipelineService {
    /// <summary>
    /// Contract implementation.
    /// </summary>
    [ServiceBehavior(
        InstanceContextMode = InstanceContextMode.Single,
        IncludeExceptionDetailInFaults = true,
        ConcurrencyMode = ConcurrencyMode.Single,
        MaxItemsInObjectGraph = Int32.MaxValue)]
    internal class BuildPipelineServiceImpl : IBuildPipelineService {

        private List<BuildArtifact> associatedArtifacts = new List<BuildArtifact>();
        private BuildOperationContext ctx;

        internal ProjectTreeOutputSnapshot Outputs { get; } = new ProjectTreeOutputSnapshot();

        public void Publish(BuildOperationContext context) {
            ctx = context;
        }

        public BuildOperationContext GetContext() {
            return ctx;
        }

        public void RecordProjectOutput(OutputFilesSnapshot snapshot) {
            Outputs[snapshot.ProjectFile] = snapshot;
        }

        public IEnumerable<OutputFilesSnapshot> GetProjectOutputs(string publisherName) {
            return Outputs.GetProjectsForTag(publisherName);
        }

        public IEnumerable<OutputFilesSnapshot> GetAllProjectOutputs() {
            return Outputs.Values;
        }

        public void RecordArtifacts(string key, IEnumerable<ArtifactManifest> manifests) {
            ctx.RecordArtifact(key, manifests.ToList());
        }

        public void PutVariable(string scope, string variableName, string value) {
            ctx.PutVariable(scope, variableName, value);
        }

        public string GetVariable(string scope, string variableName) {
            return ctx.GetVariable(scope, variableName);
        }

        public void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts) {
            if (artifacts != null) {
                associatedArtifacts.AddRange(artifacts);
            }
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return associatedArtifacts.ToArray();
        }
    }
}
