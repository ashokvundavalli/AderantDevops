using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.Services;
using Aderant.Build.VersionControl;
using Aderant.Build.VersionControl.Model;

namespace Aderant.Build {

    [Serializable]
    [DataContract]
    public class BuildOperationContext {
        [DataMember]
        private ArtifactCollection artifacts;

        [DataMember]
        private BuildMetadata buildMetadata;

        [DataMember]
        private string buildScriptsDirectory;

        [DataMember]
        private BuildStateMetadata buildStateMetadata;

        [DataMember]
        private DropPaths drops;

        [DataMember]
        private bool isDesktopBuild = true;

        [DataMember]
        // For deterministic hashing it is better if this is sorted
        private ProjectOutputSnapshot outputs;

        [DataMember]
        private string primaryDropLocation;

        [DataMember]
        private string pullRequestDropLocation;

        [IgnoreDataMember]
        private int recordArtifactCount;

        [NonSerialized]
        [IgnoreDataMember]
        private IContextualServiceProvider serviceProvider;

        [DataMember]
        private SourceTreeMetadata sourceTreeMetadata;

        [DataMember]
        private List<BuildStateFile> stateFiles;

        [DataMember]
        private BuildSwitches switches = default(BuildSwitches);

        [IgnoreDataMember]
        private int trackedProjectCount;

        [DataMember]
        private string productManifestPath;

        public BuildOperationContext() {
            Configuration = new Dictionary<object, object>();
            VariableBags = new SortedDictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            TaskIndex = -1;
            Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Environment = "";
            PipelineName = "";
            TaskName = "";
        }

        public string BuildScriptsDirectory {
            get {
                if (string.IsNullOrWhiteSpace(buildScriptsDirectory)) {
                    throw new ArgumentNullException(nameof(buildScriptsDirectory));
                }

                return buildScriptsDirectory;
            }
            set {
                value = Path.GetFullPath(value);
                buildScriptsDirectory = value;
            }
        }

        [DataMember]
        public DirectoryInfo BuildRoot { get; set; }

        [DataMember]
        public string BuildSystemDirectory { get; set; }

        public bool IsDesktopBuild {
            get { return isDesktopBuild; }
            set { isDesktopBuild = value; }
        }

        [DataMember]
        public IDictionary Configuration { get; set; }

        [DataMember]
        public FileInfo ConfigurationPath { get; set; }

        [DataMember]
        public DirectoryInfo DownloadRoot { get; set; }

        [DataMember]
        public string Environment { get; set; }

        [DataMember]
        public DirectoryInfo OutputDirectory { get; set; }

        [DataMember]
        public string PipelineName { get; set; }

        [DataMember]
        public bool Publish { get; set; }

        [DataMember]
        public DateTime StartedAt { get; set; }

        [DataMember]
        public string TaskName { get; set; }

        [DataMember]
        public int TaskIndex { get; set; }

        [DataMember]
        public IDictionary<string, IDictionary<string, string>> VariableBags { get; private set; }

        [DataMember]
        public DirectoryInfo Temp { get; set; }

        [DataMember]
        public IDictionary<string, string> Variables { get; private set; }

        public BuildMetadata BuildMetadata {
            get { return buildMetadata; }
            set {
                buildMetadata = value;

                if (value != null) {
                    if (value.BuildId > 0) {
                        IsDesktopBuild = false;
                    } else {
                        IsDesktopBuild = true;
                    }
                }
            }
        }

        public BuildSwitches Switches {
            get { return switches; }
            set { switches = value; }
        }

        internal IContextualServiceProvider ServiceProvider {
            get {
                if (serviceProvider != null) {
                    return serviceProvider;
                }

                return serviceProvider = ServiceContainer.Default;
            }
        }

        [DataMember]
        public ConfigurationToBuild ConfigurationToBuild { get; set; }

        public string PrimaryDropLocation {
            get { return primaryDropLocation; }
            set { primaryDropLocation = value; }
        }

        public string PullRequestDropLocation {
            get { return pullRequestDropLocation; }
            set { pullRequestDropLocation = value; }
        }

        public SourceTreeMetadata SourceTreeMetadata {
            get { return sourceTreeMetadata; }
            set { sourceTreeMetadata = value; }
        }

        public BuildStateMetadata BuildStateMetadata {
            get { return buildStateMetadata; }
            set { buildStateMetadata = value; }
        }

        public string ProductManifestPath {
            get { return productManifestPath; }
            set { productManifestPath = value; }
        }

        /// <summary>
        /// The state file this build is using (if any).
        /// This indicates if we are reusing an existing build.
        /// </summary>
        public List<BuildStateFile> StateFiles {
            get { return stateFiles; }
            set { stateFiles = value; }
        }

        public DropPaths Drops {
            get { return drops ?? (drops = new DropPaths()); }
        }

        internal void RecordArtifact(string key, ICollection<ArtifactManifest> manifests) {
            InitArtifacts();
            artifacts[key] = manifests;
        }

        /// <summary>
        /// Creates a new instance of T.
        /// </summary>
        public T GetService<T>() where T : class {
            var svc = ServiceProvider.GetService(typeof(T));
            return (T)svc;
        }

        public object GetService(string contract) {
            var svc = ServiceProvider.GetService<object>(this, contract, null);
            return svc;
        }

        internal DependencyRelationshipProcessing GetRelationshipProcessingMode() {
            DependencyRelationshipProcessing relationshipProcessing = DependencyRelationshipProcessing.None;
            if (Switches.Downstream) {
                relationshipProcessing = DependencyRelationshipProcessing.Direct;
            }

            if (Switches.Transitive) {
                relationshipProcessing = DependencyRelationshipProcessing.Transitive;
            }

            return relationshipProcessing;
        }

        public ChangesToConsider GetChangeConsiderationMode() {
            ChangesToConsider mode = ChangesToConsider.None;
            if (Switches.PendingChanges) {
                mode = ChangesToConsider.PendingChanges;
            }

            if (Switches.Everything) {
                mode = ChangesToConsider.Branch;
            }

            // TODO: Drive this from a PR comment?
            if (buildMetadata != null) {
                if (buildMetadata.IsPullRequest) {
                    return ChangesToConsider.PendingChanges;
                }
            }

            if (StateFiles != null) {
                return ChangesToConsider.PendingChanges;
            }

            return mode;
        }

        public void PutVariable(string scope, string variableName, string value) {
            var bags = VariableBags;

            IDictionary<string, string> bag;
            if (!bags.TryGetValue(scope, out bag)) {
                bag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bags[scope] = bag;
            }

            bag[variableName] = value;
        }

        public string GetVariable(string scope, string variableName) {
            ErrorUtilities.IsNotNull(scope, nameof(scope));
            ErrorUtilities.IsNotNull(variableName, nameof(variableName));

            var bags = VariableBags;

            IDictionary<string, string> bag;
            if (bags.TryGetValue(scope, out bag)) {
                string value;
                bag.TryGetValue(variableName, out value);

                return value;
            }

            return null;
        }

        public string GetDropLocation(string publisherName) {
            return BucketPathBuilder.BuildDropLocation(publisherName, this);
        }

        internal OutputFilesSnapshot RecordProjectOutputs(
            Guid projectGuid,
            string sourcesDirectory,
            string projectFile,
            string[] projectOutputs,
            string outputPath,
            string intermediateDirectory,
            IReadOnlyCollection<string> projectTypeGuids = null,
            string testProjectType = null,
            string[] references = null) {
            ErrorUtilities.IsNotNull(sourcesDirectory, nameof(sourcesDirectory));

            InitOutputs();

            var tracker = new ProjectOutputSnapshotFactory(outputs) {
                SourcesDirectory = sourcesDirectory,
                ProjectFile = projectFile,
                ProjectOutputs = projectOutputs,
                OutputPath = outputPath,
                IntermediateDirectory = intermediateDirectory,
                ProjectTypeGuids = projectTypeGuids,
                TestProjectType = testProjectType,
                References = references,
            };

            return tracker.TakeSnapshot(projectGuid);
        }

        private void InitOutputs() {
            Interlocked.Increment(ref trackedProjectCount);

            if (outputs == null) {
                outputs = new ProjectOutputSnapshot();
            }
        }

        /// <summary>
        /// Returns the outputs for all projects seen by the build.
        /// Keyed by project file.
        /// </summary>
        internal ProjectOutputSnapshot GetProjectOutputs() {
            return outputs;
        }

        /// <summary>
        /// Returns the outputs for a specific publisher.
        /// </summary>
        internal ProjectOutputSnapshot GetProjectOutputs(string publisherName) {
            if (outputs != null) {
                return outputs.GetProjectsForTag(publisherName);
            }

            return null;
        }

        /// <summary>
        /// Returns the artifacts for a given publisher.
        /// Keyed by publisher.
        /// </summary>
        internal ArtifactCollection GetArtifacts() {
            return artifacts;
        }

        internal void RecordArtifact(string publisherName, string artifactId, ICollection<ArtifactItem> files) {
            InitArtifacts();

            ICollection<ArtifactManifest> manifests;

            if (!artifacts.TryGetValue(publisherName, out manifests)) {
                manifests = new List<ArtifactManifest>();
                artifacts[publisherName] = manifests;
            }

            manifests.Add(
                new ArtifactManifest {
                    Id = artifactId,
                    InstanceId = Guid.NewGuid(),
                    Files = files,
                });
        }

        private void InitArtifacts() {
            if (artifacts == null) {
                artifacts = new ArtifactCollection();
            }

            Interlocked.Increment(ref recordArtifactCount);
        }

        public BuildStateFile GetStateFile(string bucketTag) {
            foreach (var file in StateFiles) {
                if (string.Equals(file.BucketId.Tag, bucketTag, StringComparison.OrdinalIgnoreCase)) {
                    return file;
                }
            }

            return null;
        }

        internal void RecordProjectOutputs(OutputFilesSnapshot snapshot) {
            InitOutputs();

            outputs[snapshot.ProjectFile] = snapshot;
        }
    }

    [Serializable]
    [DataContract]
    public class DropPaths {

        [DataMember]
        public string PrimaryDropLocation { get; set; }

        [DataMember]
        public string PullRequestDropLocation { get; set; }

        [DataMember]
        public string XamlBuildDropLocation { get; set; }
    }

    [Serializable]
    [CollectionDataContract]
    internal class ArtifactCollection : SortedDictionary<string, ICollection<ArtifactManifest>> {

        public ArtifactCollection()
            : base(StringComparer.OrdinalIgnoreCase) {
        }

        public ArtifactCollection GetArtifactsForTag(string tag) {
            var items = this.Where(m => string.Equals(m.Key, tag, StringComparison.OrdinalIgnoreCase));

            var collection = new ArtifactCollection();
            foreach (var item in items) {
                collection.Add(item.Key, item.Value);
            }

            return collection;
        }
    }

    [Serializable]
    [CollectionDataContract]
    internal class ProjectOutputSnapshot : SortedDictionary<string, OutputFilesSnapshot> {

        public ProjectOutputSnapshot()
            : base(StringComparer.OrdinalIgnoreCase) {
        }

        public ProjectOutputSnapshot(IDictionary<string, OutputFilesSnapshot> dictionary)
            : base(dictionary, StringComparer.OrdinalIgnoreCase) {
        }

        public ProjectOutputSnapshot GetProjectsForTag(string tag) {
            var items = this.Where(m => string.Equals(m.Value.Directory, tag, StringComparison.OrdinalIgnoreCase));

            var collection = new ProjectOutputSnapshot();
            foreach (var item in items) {
                collection.Add(item.Key, item.Value);
            }

            return collection;
        }
    }

    [Serializable]
    [DataContract]
    internal class ArtifactManifest {

        [DataMember]
        public string Id { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid InstanceId { get; set; }

        [DataMember]
        public ICollection<ArtifactItem> Files { get; set; }
    }

    [Serializable]
    [DataContract]
    internal class ArtifactItem {

        [DataMember]
        public string File { get; set; }
    }

    [DataContract]
    [Serializable]
    internal class OutputFilesSnapshot {

        [DataMember]
        public string ProjectFile { get; set; }

        [DataMember]
        public string[] FilesWritten { get; set; }

        [DataMember]
        public string OutputPath { get; set; }

        [DataMember]
        public string Origin { get; set; }

        [DataMember]
        public string Directory { get; set; }

        [DataMember]
        public bool IsTestProject { get; set; }

        [DataMember]
        public Guid ProjectGuid { get; set; }
    }

    [Serializable]
    [DataContract]
    public sealed class SourceTreeMetadata {

        [DataMember]
        public string CommonAncestor { get; set; }

        [DataMember]
        public IReadOnlyCollection<BucketId> BucketIds { get; set; }

        [DataMember]
        public IReadOnlyCollection<SourceChange> Changes { get; set; }

        [DataMember]
        public string NewCommitDescription { get; set; }

        [DataMember]
        public string OldCommitDescription { get; set; }

        public BucketId GetBucket(string tag) {
            foreach (var bucket in BucketIds) {
                if (string.Equals(bucket.Tag, tag, StringComparison.OrdinalIgnoreCase)) {
                    return bucket;
                }
            }

            return null;
        }

        public IReadOnlyCollection<BucketId> GetBuckets() {
            return BucketIds.Where(b => !b.IsRoot).ToList();
        }
    }

    [Serializable]
    [DataContract]
    public struct BuildSwitches {

        [DataMember]
        public bool PendingChanges { get; set; }

        [DataMember]
        public bool Downstream { get; set; }

        [DataMember]
        public bool Transitive { get; set; }

        [DataMember]
        public bool Everything { get; set; }

        [DataMember]
        public bool Clean { get; set; }

        [DataMember]
        public bool Release { get; set; }

        [DataMember]
        public bool DryRun { get; set; }

        [DataMember]
        public bool Resume { get; set; }

        [CreateProperty]
        public bool SkipCompile { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class CreatePropertyAttribute : Attribute {
    }

}
