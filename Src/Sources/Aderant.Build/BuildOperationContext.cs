using System;
using System.Collections.Generic;
using System.Globalization;
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
using ProtoBuf;
using ProtoBuf.Meta;

namespace Aderant.Build {

    [DataContract]
    [ProtoContract]
    public class BuildOperationContext {

        [DataMember]
        private ArtifactCollection artifacts;

        [DataMember(EmitDefaultValue = false)]
        private string artifactStagingDirectory;

        [DataMember]
        private BuildMetadata buildMetadata;

        [DataMember]
        private string buildScriptsDirectory;

        [DataMember]
        private BuildStateMetadata buildStateMetadata;

        [DataMember]
        private string buildSystemDirectory;

        [DataMember]
        private DropPaths drops;

        [DataMember]
        private bool isDesktopBuild = true;

        [DataMember]
        private ProjectOutputSnapshot outputs;

        [DataMember(EmitDefaultValue = false)]
        private string productManifestPath;

        [IgnoreDataMember]
        private int recordArtifactCount;

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

        private ICollection<string> writtenStateFiles;

        static BuildOperationContext() {
            RuntimeTypeModel.Default.Add(typeof(BuildOperationContext), false)
                .Add(
                    nameof(artifacts),
                    nameof(artifactStagingDirectory),
                    //
                    nameof(buildScriptsDirectory),
                    nameof(buildMetadata),
                    nameof(buildStateMetadata),
                    nameof(buildSystemDirectory),
                    //
                    nameof(ConfigurationToBuild),
                    //
                    nameof(drops),
                    nameof(DownloadRoot),
                    //
                    nameof(Environment),
                    //
                    nameof(isDesktopBuild),
                    //
                    nameof(outputs),
                    //
                    nameof(productManifestPath),
                    nameof(PipelineName),
                    //
                    nameof(sourceTreeMetadata),
                    nameof(stateFiles),
                    nameof(switches),
                    nameof(ScopedVariables),

                    nameof(writtenStateFiles)
                );

            var schema = RuntimeTypeModel.Default.GetSchema(typeof(BuildOperationContext));
            var a = RuntimeTypeModel.Default.GetSchema(typeof(BuildArtifact));
        }

        public BuildOperationContext() {
            ScopedVariables = new SortedDictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Environment = "";
            PipelineName = "";
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

        public string BuildSystemDirectory {
            get { return buildSystemDirectory; }
            set { buildSystemDirectory = value; }
        }

        public bool IsDesktopBuild {
            get { return isDesktopBuild; }
            set { isDesktopBuild = value; }
        }

        [DataMember]
        public string DownloadRoot { get; set; }

        [DataMember]
        public string Environment { get; set; }

        [DataMember]
        public string PipelineName { get; set; }

        [DataMember]
        public DateTime StartedAt { get; set; }

        [DataMember]
        public IDictionary<string, IDictionary<string, string>> ScopedVariables { get; private set; }

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

        public string ArtifactStagingDirectory {
            get { return artifactStagingDirectory; }
            set {
                if (value != null) {
                    value = Path.GetFullPath(value);
                    value = value.TrimEnd(Path.DirectorySeparatorChar);
                }

                artifactStagingDirectory = value;
            }
        }

        public ICollection<string> WrittenStateFiles {
            get { return writtenStateFiles ?? (writtenStateFiles = new List<string>()); }
            set { writtenStateFiles = value; }
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
            var bags = ScopedVariables;

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

            var bags = ScopedVariables;

            IDictionary<string, string> bag;
            if (bags.TryGetValue(scope, out bag)) {
                string value;
                bag.TryGetValue(variableName, out value);

                return value;
            }

            return null;
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
            ErrorUtilities.IsNotNull(publisherName, nameof(publisherName));
            ErrorUtilities.IsNotNull(artifactId, nameof(artifactId));

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
            var files = StateFiles;
            if (files != null)
                foreach (var file in files) {
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

    internal class ArtifactStagingPathBuilder {
        private readonly BuildOperationContext context;
        private string stagingDirectory;

        public ArtifactStagingPathBuilder(BuildOperationContext context) {
            ErrorUtilities.IsNotNull(context, nameof(context));

            this.context = context;
            this.stagingDirectory = Path.Combine(context.ArtifactStagingDirectory, "_artifacts");
        }

        public string BuildPath(string name) {
            BucketId bucket = context.SourceTreeMetadata.GetBucket(name);

            return Path.Combine(
                stagingDirectory,
                bucket != null ? bucket.Id ?? string.Empty : string.Empty,
                context.BuildMetadata.BuildId.ToString(CultureInfo.InvariantCulture));

        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    public class DropPaths {

        [DataMember]
        public string PrimaryDropLocation { get; set; }

        [DataMember]
        public string PullRequestDropLocation { get; set; }

        [DataMember]
        public string XamlBuildDropLocation { get; set; }
    }

    [CollectionDataContract]
    [ProtoContract]
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

    [CollectionDataContract]
    [ProtoContract]
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

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    internal class ArtifactManifest {

        [DataMember]
        public string Id { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid InstanceId { get; set; }

        [DataMember]
        public ICollection<ArtifactItem> Files { get; set; }
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    internal class ArtifactItem {

        [DataMember]
        public string File { get; set; }
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
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

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
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

        /// <summary>
        /// Gets a bucket for a friendly name.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
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
