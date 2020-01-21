﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;

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
        // The adapter used for writing to the host UI
        private readonly IConsoleAdapter consoleAdapter;

        private ArtifactCollection artifacts;

        private List<BuildArtifact> associatedArtifacts = new List<BuildArtifact>();

        private BuildOperationContext ctx;
        private List<BuildDirectoryContribution> directoryMetadata = new List<BuildDirectoryContribution>();
        private List<OnDiskProjectInfo> projects = new List<OnDiskProjectInfo>();
        private Dictionary<string, IReadOnlyCollection<TrackedInputFile>> trackedDependenciesBySolutionRoot = new Dictionary<string, IReadOnlyCollection<TrackedInputFile>>(StringComparer.OrdinalIgnoreCase);

        public BuildPipelineServiceImpl() {
        }

        public BuildPipelineServiceImpl(IConsoleAdapter consoleAdapter) {
            this.consoleAdapter = consoleAdapter;
        }

        private List<string> ImpactedProjects = new List<string>();
        private Dictionary<string, List<string>> RelatedFiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        internal ProjectTreeOutputSnapshot Outputs { get; } = new ProjectTreeOutputSnapshot();

        public void Publish(BuildOperationContext context) {
            if (string.IsNullOrEmpty(context.BuildRoot)) {
                if (ctx != null && !string.IsNullOrEmpty(ctx.BuildRoot)) {
                    context.BuildRoot = ctx.BuildRoot;
                }
            }
            ctx = context;
        }

        public BuildOperationContext GetContext() {
            return ctx;
        }

        public void RecordProjectOutputs(ProjectOutputSnapshot snapshot) {
            Outputs[snapshot.ProjectFile] = snapshot;
        }

        public void RecordImpactedProjects(IEnumerable<string> impactedProjects) {
            ImpactedProjects = impactedProjects.ToList();
        }

        public void RecordRelatedFiles(Dictionary<string, List<string>> relatedFiles) {            
            if (RelatedFiles == null) {
                RelatedFiles = relatedFiles;
            } else {
                foreach (var relatedFile in relatedFiles.Keys) {
                    if (RelatedFiles.ContainsKey(relatedFile)) {
                        RelatedFiles[relatedFile] = RelatedFiles[relatedFile].Union(relatedFiles[relatedFile]).ToList();
                    } else {
                        RelatedFiles.Add(relatedFile, relatedFiles[relatedFile]);
                    }                    
                }
            }
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container) {
            return Outputs.GetProjectsForTag(container);
        }

        public Dictionary<string, List<string>> GetRelatedFiles() {
            return RelatedFiles;
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectSnapshots() {
            return Outputs.Values;
        }

        public IEnumerable<string> GetImpactedProjects() {
            return ImpactedProjects;
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

        public void TrackProject(OnDiskProjectInfo onDiskProject) {
            projects.Add(onDiskProject);
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects() {
            return projects;
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects(IEnumerable<Guid> ids) {
            ErrorUtilities.IsNotNull(ids, nameof(ids));

            return projects.Where(p => ids.Contains(p.ProjectGuid));
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

        public void SetProgress(string currentOperation, string activity, string statusDescription) {
            if (consoleAdapter != null) {
                Task.Run(
                    () => {
                        if (consoleAdapter != null) {
                            consoleAdapter.RaiseProgressChanged(currentOperation, activity, statusDescription);
                        }
                    });
            }
        }

        /// <summary>
        /// Notifies the service it should track a set of files.
        /// </summary>
        public void TrackInputFileDependencies(string solutionRoot, IReadOnlyCollection<TrackedInputFile> fileDependencies) {
            trackedDependenciesBySolutionRoot[solutionRoot] = fileDependencies;
        }

        /// <summary>
        /// Retrieves a collection of tracked files from the service. If the service has any files for the given key
        /// they are removed from the internal buffer.
        /// </summary>
        public IReadOnlyCollection<TrackedInputFile> ClaimTrackedInputFiles(string tag) {
            IReadOnlyCollection<TrackedInputFile> trackedFiles;
            if (trackedDependenciesBySolutionRoot.TryGetValue(tag, out trackedFiles)) {
                trackedDependenciesBySolutionRoot.Remove(tag);
            }

            return trackedFiles;
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

        public void AddBuildDirectoryContributor(BuildDirectoryContribution buildDirectoryContribution) {
            directoryMetadata.Add(buildDirectoryContribution);
        }

        public IReadOnlyCollection<BuildDirectoryContribution> GetContributors() {
            return directoryMetadata;
        }

        private void InitArtifacts() {
            if (artifacts == null) {
                artifacts = new ArtifactCollection();
            }
        }
    }
}