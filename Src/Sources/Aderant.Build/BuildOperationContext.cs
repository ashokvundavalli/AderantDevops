using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.StateTracking;
using Aderant.Build.VersionControl;
using Aderant.Build.VersionControl.Model;
using ProtoBuf;

namespace Aderant.Build {

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class BuildOperationContext {

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
        private DropLocationInfo drops;

        [DataMember]
        private bool isDesktopBuild = true;

        [DataMember(EmitDefaultValue = false)]
        private string productManifestPath;

        [DataMember]
        private SourceTreeMetadata sourceTreeMetadata;

        [DataMember]
        private DateTime startedAt;

        [DataMember]
        private List<BuildStateFile> stateFiles;

        [DataMember]
        private BuildSwitches switches = default(BuildSwitches);

        [DataMember]
        private ICollection<string> writtenStateFiles;

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
        public string[] DirectoriesToBuild { get; set; }

        [DataMember]
        public string BuildRoot { get; set; }

        [DataMember]
        public string LogFile { get; set; }

        public string BuildSystemDirectory {
            get { return buildSystemDirectory; }
            set { buildSystemDirectory = value; }
        }

        [IgnoreDataMember]
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
        public string BuildStatus { get; set; }

        [DataMember]
        public string BuildStatusReason { get; set; }

     public DateTime StartedAt {
            get { return startedAt; }
            set {
                startedAt = value;
                if (BuildStatus == null) {
                    BuildStatus = "Started";
                }
            }
        }

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

        public DropLocationInfo DropLocationInfo {
            get { return drops ?? (drops = new DropLocationInfo()); }
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

        /// <summary>
        /// The paths to state files produced during a build.
        /// </summary>
        public ICollection<string> WrittenStateFiles {
            get { return writtenStateFiles ?? (writtenStateFiles = new List<string>()); }
            set { writtenStateFiles = value; }
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
            if (IsDesktopBuild) {
                mode = ChangesToConsider.PendingChanges;
            }

            if (Switches.Branch) {
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
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DataContract]
    public class DropLocationInfo {

        [DataMember]
        public string PrimaryDropLocation { get; set; }

        [DataMember]
        public string BuildCacheLocation { get; set; }

        [DataMember]
        public string PullRequestDropLocation { get; set; }

        [DataMember]
        public string XamlBuildDropLocation { get; set; }
    }

    [CollectionDataContract]
    [ProtoContract]
    internal class ArtifactCollection : SortedList<string, ICollection<ArtifactManifest>> {

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
    internal class ProjectTreeOutputSnapshot : SortedList<string, ProjectOutputSnapshot> {

        public ProjectTreeOutputSnapshot()
            : base(StringComparer.OrdinalIgnoreCase) {
        }

        public ProjectTreeOutputSnapshot(IDictionary<string, ProjectOutputSnapshot> dictionary)
            : base(dictionary, StringComparer.OrdinalIgnoreCase) {
        }

        public IEnumerable<ProjectOutputSnapshot> GetProjectsForTag(string tag) {
            return this.Where(m => string.Equals(m.Value.Directory, tag, StringComparison.OrdinalIgnoreCase)).Select(s => s.Value);
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
    internal class ProjectOutputSnapshot {

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

        [DataMember]
        public string CommonCommit { get; set; }

        /// <summary>
        /// Gets a bucket for a friendly name.
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public BucketId GetBucket(string tag) {
            if (BucketIds != null) {
                foreach (var bucket in BucketIds) {
                    if (string.Equals(bucket.Tag, tag, StringComparison.OrdinalIgnoreCase)) {
                        return bucket;
                    }
                }
            }

            return null;
        }

        public IReadOnlyCollection<BucketId> GetBuckets() {
            return BucketIds.Where(b => !b.IsRoot).ToList();
        }
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct BuildSwitches {

        [DataMember]
        public bool Downstream { get; set; }

        [DataMember]
        public bool Transitive { get; set; }

        [DataMember]
        public bool Branch { get; set; }

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
