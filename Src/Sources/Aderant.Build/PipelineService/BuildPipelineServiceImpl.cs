using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
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
        ConcurrencyMode = ConcurrencyMode.Multiple,
        MaxItemsInObjectGraph = Int32.MaxValue)]
    internal class BuildPipelineServiceImpl : IBuildPipelineService {
        // The adapter used for writing to the host UI
        private readonly IConsoleAdapter consoleAdapter;

        private ArtifactCollection artifacts;

        private BuildOperationContext ctx;

        // These types are generally used with ToList/new List() to avoid any serialization issues
        private readonly ConcurrentBag<BuildArtifact> associatedArtifacts = new ConcurrentBag<BuildArtifact>();
        private readonly ConcurrentBag<BuildDirectoryContribution> directoryMetadata = new ConcurrentBag<BuildDirectoryContribution>();
        private readonly ConcurrentBag<OnDiskProjectInfo> projects = new ConcurrentBag<OnDiskProjectInfo>();
        private readonly ConcurrentDictionary<string, IReadOnlyCollection<TrackedInputFile>> trackedDependenciesBySolutionRoot = new ConcurrentDictionary<string, IReadOnlyCollection<TrackedInputFile>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentBag<string> impactedProjects = new ConcurrentBag<string>();
        private readonly ConcurrentDictionary<string, List<string>> relatedFiles = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private readonly ReaderWriterLockSlim contextLock = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim artifactsLock = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim outputsLock = new ReaderWriterLockSlim();

        public BuildPipelineServiceImpl() {
        }

        public BuildPipelineServiceImpl(IConsoleAdapter consoleAdapter) {
            this.consoleAdapter = consoleAdapter;
        }

        internal ProjectTreeOutputSnapshot Outputs { get; } = new ProjectTreeOutputSnapshot();

        public void Publish(BuildOperationContext context) {
            try {
                contextLock.EnterUpgradeableReadLock();

                if (string.IsNullOrEmpty(context.BuildRoot)) {
                    if (ctx != null && !string.IsNullOrEmpty(ctx.BuildRoot)) {
                        context.BuildRoot = ctx.BuildRoot;
                    }
                }

                try {
                    contextLock.EnterWriteLock();
                    ctx = context;
                } finally {
                    contextLock.ExitWriteLock();
                }
            } finally {
                contextLock.ExitUpgradeableReadLock();
            }
        }

        public BuildOperationContext GetContext() {
            try {
                contextLock.EnterReadLock();
                return ctx;
            } finally {
                contextLock.ExitReadLock();
            }
        }

        public void RecordProjectOutputs(ProjectOutputSnapshot snapshot) {
            try {
                outputsLock.EnterWriteLock();

                Outputs[snapshot.ProjectFile] = snapshot;
            } finally {
                outputsLock.ExitWriteLock();
            }
        }

        public void RecordImpactedProjects(IEnumerable<string> impactedProjects) {
            foreach (var item in impactedProjects) {
                this.impactedProjects.Add(item);
            }
        }

        public void RecordRelatedFiles(Dictionary<string, List<string>> relatedFiles) {
            foreach (var relatedFile in relatedFiles.Keys) {
                if (this.relatedFiles.ContainsKey(relatedFile)) {
                    this.relatedFiles[relatedFile] = this.relatedFiles[relatedFile].Union(relatedFiles[relatedFile]).ToList();
                } else {
                    this.relatedFiles.TryAdd(relatedFile, relatedFiles[relatedFile]);
                }
            }
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectOutputs(string container) {
            try {
                outputsLock.EnterReadLock();

                return Outputs.GetProjectsForTag(container).ToList();
            } finally {
                outputsLock.ExitReadLock();
            }
        }

        public Dictionary<string, List<string>> GetRelatedFiles() {
            return new Dictionary<string, List<string>>(relatedFiles);
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectSnapshots() {
            try {
                outputsLock.EnterReadLock();

                return Outputs.Values.ToList();
            } finally {
                outputsLock.ExitReadLock();
            }
        }

        public IEnumerable<string> GetImpactedProjects() {
            return new List<string>(impactedProjects);
        }

        public void RecordArtifacts(string container, IEnumerable<ArtifactManifest> manifests) {
            try {
                artifactsLock.EnterWriteLock();

                InitArtifacts();

                ICollection<ArtifactManifest> existing;
                if (artifacts.TryGetValue(container, out existing)) {
                    foreach (var artifactManifest in manifests) {
                        existing.Add(artifactManifest);
                    }
                } else {
                    artifacts[container] = manifests.ToList();
                }
            } finally {
                artifactsLock.ExitWriteLock();
            }
        }

        public void PutVariable(string scope, string variableName, string value) {
            try {
                contextLock.EnterWriteLock();

                ctx.PutVariable(scope, variableName, value);
            } finally {
                contextLock.ExitWriteLock();
            }
        }

        public string GetVariable(string scope, string variableName) {
            try {
                contextLock.EnterReadLock();

                return ctx.GetVariable(scope, variableName);
            } finally {
                contextLock.ExitReadLock();
            }
        }

        public void TrackProject(OnDiskProjectInfo onDiskProject) {
            projects.Add(onDiskProject);
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects() {
            return new List<OnDiskProjectInfo>(projects);
        }

        public IEnumerable<OnDiskProjectInfo> GetTrackedProjects(IEnumerable<Guid> ids) {
            ErrorUtilities.IsNotNull(ids, nameof(ids));

            return projects.Where(p => ids.Contains(p.ProjectGuid)).ToList();
        }

        public IEnumerable<ArtifactManifest> GetArtifactsForContainer(string container) {
            if (artifacts != null) {
                try {
                    artifactsLock.EnterReadLock();

                    var artifactsForTag = artifacts.GetArtifactsForTag(container);

                    if (artifactsForTag != null) {
                        return artifactsForTag.SelectMany(s => s.Value);
                    }
                } finally {
                    artifactsLock.ExitReadLock();
                }
            }

            return null;
        }

        public object[] Ping() {
            return new object[] {
                (byte) 1
            };
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
                trackedDependenciesBySolutionRoot.TryRemove(tag, out _);
            }

            return trackedFiles;
        }

        public void AssociateArtifacts(IEnumerable<BuildArtifact> artifacts) {
            if (artifacts != null) {
                foreach (var item in artifacts) {
                    associatedArtifacts.Add(item);
                }
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
            return directoryMetadata.ToList();
        }

        private void InitArtifacts() {
            if (artifacts == null) {
                artifacts = new ArtifactCollection();
            }
        }
    }
}