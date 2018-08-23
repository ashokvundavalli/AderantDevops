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
        private BuildOperationContext ctx;

        private List<BuildArtifact> vsoArtifacts = new List<BuildArtifact>();

        public void Publish(BuildOperationContext context) {
            ctx = context;
        }

        public BuildOperationContext GetContext() {
            return ctx;
        }

        public void RecordProjectOutputs(OutputFilesSnapshot snapshot) {
            ctx.RecordProjectOutputs(snapshot);
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

        public void AssociateArtifact(IEnumerable<BuildArtifact> artifacts) {
            if (artifacts != null) {
                vsoArtifacts.AddRange(artifacts);
            }
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return vsoArtifacts.ToArray();
        }
    }
}
