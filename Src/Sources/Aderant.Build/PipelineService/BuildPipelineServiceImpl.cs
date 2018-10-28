﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Aderant.Build.Model;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;

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

        private ArtifactCollection artifacts;

        private List<BuildArtifact> associatedArtifacts = new List<BuildArtifact>();

        private BuildOperationContext ctx;
        private List<TrackedProject> projects = new List<TrackedProject>();

        internal ProjectTreeOutputSnapshot Outputs { get; } = new ProjectTreeOutputSnapshot();

        public void Publish(BuildOperationContext context) {
            ctx = context;
        }

        public BuildOperationContext GetContext() {
            return ctx;
        }

        public void RecordProjectOutputs(ProjectOutputSnapshot snapshot) {
            Outputs[snapshot.ProjectFile] = snapshot;
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container) {
            return Outputs.GetProjectsForTag(container);
        }

        public IEnumerable<ProjectOutputSnapshot> GetAllProjectOutputs() {
            return Outputs.Values;
        }

        public void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests) {
            InitArtifacts();

            ICollection<ArtifactManifest> existing;
            if (artifacts.TryGetValue(container, out existing)) {
                foreach (var artifactManifest in manifests) {
                    existing.Add(artifactManifest);
                }
            } else {
                artifacts[container] = manifests.ToList();
            }
        }

        public void PutVariable(string scope, string variableName, string value) {
            ctx.PutVariable(scope, variableName, value);
        }

        public string GetVariable(string scope, string variableName) {
            return ctx.GetVariable(scope, variableName);
        }

        public void TrackProject(TrackedProject trackedProject) {
            projects.Add(trackedProject);
        }

        public IEnumerable<TrackedProject> GetTrackedProjects() {
            return projects;
        }

        public IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container) {
            if (artifacts != null) {
                var artifactsForTag = artifacts.GetArtifactsForTag(container);

                if (artifactsForTag != null) {
                    return artifactsForTag.SelectMany(s => s.Value);
                }
            }

            return null;
        }

        public object[] Ping() {
            return new object[] { (byte)1 };
        }

        public void SetStatus(string status, string reason) {
            if (status != null) {
                ctx.BuildStatus = status;
            }

            if (reason != null) {
                ctx.BuildStatusReason = reason;
            }
        }

        public void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts) {
            if (artifacts != null) {
                associatedArtifacts.AddRange(artifacts);
            }
        }

        public BuildArtifact[] GetAssociatedArtifacts() {
            return associatedArtifacts.ToArray();
        }

        public void Dispose() {
        }

        private void InitArtifacts() {
            if (artifacts == null) {
                artifacts = new ArtifactCollection();
            }
        }
    }
}
