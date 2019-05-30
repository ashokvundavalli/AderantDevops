using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields, SkipConstructor = true)]
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
        private bool isDesktopBuild;

        [DataMember(EmitDefaultValue = false)]
        private string productManifestPath;

        [DataMember]
        private IDictionary<string, IDictionary<string, string>> scopedVariables;

        [DataMember]
        private SourceTreeMetadata sourceTreeMetadata;

        [DataMember]
        private DateTime startedAt;

        [DataMember]
        private List<BuildStateFile> stateFiles;

        [DataMember]
        private BuildSwitches switches = default(BuildSwitches);

        [DataMember]
        private IDictionary<string, string> variables;

        [DataMember]
        private ICollection<string> writtenStateFiles;

        [DataMember]
        private string buildRoot;

        public BuildOperationContext() {
            Environment = "";
            PipelineName = "";
            IsDesktopBuild = true;
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
        private string[] include;
        /// <summary>
        /// Includes solutions and projects found under these paths into the build tree.
        /// </summary>        
        public string[] Include {
            get {
                return include ?? new string[] { BuildRoot };
            }
            set {
                this.include = value;
            }
        }

        [DataMember]
        private string[] exclude;
        /// <summary>
        /// Excludes solutions and projects found under these paths from build tree.
        /// </summary>
        public string[] Exclude {
            get {
                return exclude ?? new string[] { };
            }
            set {
                this.exclude = value;
            }
        }

        public string BuildRoot {
            get { return buildRoot; }
            set {
                if (!string.IsNullOrEmpty(buildRoot)) {
                    ErrorUtilities.VerifyThrowArgument(!string.IsNullOrEmpty(value), "BuildRoot cannot be unset", null);
                }

                buildRoot = value;
            }
        }

        [DataMember]
        public string LogFile { get; set; }

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

        public IDictionary<string, IDictionary<string, string>> ScopedVariables {
            get { return scopedVariables ?? (scopedVariables = new SortedDictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)); }
        }

        public IDictionary<string, string> Variables {
            get { return variables ?? (variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)); }
        }

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

        /// <summary>
        /// Information from the build cache that is applicable to the current tree.
        /// </summary>
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
        /// A non-null value here indicates if we are reusing an existing build.
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
            ErrorUtilities.IsNotNull(variableName, nameof(variableName));

            if (string.IsNullOrEmpty(scope)) {
                Variables[variableName] = value;
                return;
            }

            var bags = ScopedVariables;

            IDictionary<string, string> bag;
            if (!bags.TryGetValue(scope, out bag)) {
                bag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bags[scope] = bag;
            }

            bag[variableName] = value;
        }

        public string GetVariable(string scope, string variableName) {
            ErrorUtilities.IsNotNull(variableName, nameof(variableName));

            if (string.IsNullOrEmpty(scope)) {
                string value;
                Variables.TryGetValue(variableName, out value);

                return value;
            }

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

        /// <summary>
        /// The stable natural key of the artifact manifest.
        /// </summary>
        [DataMember]
        public string Id { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid InstanceId { get; set; }

        [DataMember]
        public ICollection<ArtifactItem> Files { get; set; }
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [DebuggerDisplay("{" + nameof(File) + "}")]
    internal class ArtifactItem {

        [DataMember]
        public string File { get; set; }
    }

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    internal class ProjectOutputSnapshot {
        [DataMember(Name = nameof(Directory))]
        private string directory;

        private string outputPath;

        public ProjectOutputSnapshot() {
        }

        [DataMember]
        public string ProjectFile { get; set; }

        [DataMember]
        public string[] FilesWritten { get; set; }

        [DataMember]
        public string OutputPath {
            get { return outputPath; }
            set {
                value = value.NormalizeTrailingSlashes();
                outputPath = value;
            }
        }

        [DataMember]
        public string Origin { get; set; }

        /// <summary>
        /// The name of the directory within the source tree.
        /// </summary>
        public string Directory {
            get { return directory; }
            set {
                if (value != null) {
                    if (Path.IsPathRooted(value)) {
                        throw new InvalidOperationException("Corrupted model. Directory must not be rooted.");
                    }
                }

                directory = value;
            }
        }

        /// <summary>
        /// Indicates if this is a test project.
        /// </summary>
        [DataMember]
        public bool IsTestProject { get; set; }

        /// <summary>
        /// The unique project guid - used to identify this project file within the source tree
        /// </summary>
        [DataMember]
        public Guid ProjectGuid { get; set; }
    }

    internal class ProjectOutputSnapshotWithFullPath : ProjectOutputSnapshot {
        public ProjectOutputSnapshotWithFullPath(ProjectOutputSnapshot snapshot) {
            ProjectFile = snapshot.ProjectFile;
            FilesWritten = snapshot.FilesWritten;
            OutputPath = snapshot.OutputPath;
            Origin = snapshot.Origin;
            Directory = snapshot.Directory;
            IsTestProject = snapshot.IsTestProject;
            ProjectGuid = snapshot.ProjectGuid;
        }

        /// <summary>
        /// Gets or sets the project file absolute path.
        /// </summary>
        /// <value>The project file absolute path.</value>
        [DataMember]
        public string ProjectFileAbsolutePath { get; set; }

        /// <summary>
        /// Gets the just the file names of the output items.
        /// </summary>
        public string[] FileNamesWritten { get; private set; }

        /// <summary>
        /// Populates the see <see cref="FileNamesWritten" /> property.
        /// </summary>
        public void BuildFileNamesWritten() {
            FileNamesWritten = FilesWritten.Select(
                file => {
                    if (file.StartsWith(OutputPath, StringComparison.OrdinalIgnoreCase)) {
                        return file.Remove(0, OutputPath.Length);
                    }

                    return file;
                }
            ).ToArray();
        }
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

        /// <summary>
        /// Returns the directory build tree directory identifiers (SHA1).
        /// </summary>
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
        public bool Resume { get; set; }

        [DataMember]
        public bool ChangedFilesOnly { get; set; }

        [DataMember]
        public bool SkipCompile { get; set; }

        [DataMember]
        public bool RestrictToProvidedPaths { get; set; }

        [DataMember]
        public bool ExcludeTestProjects { get; set; }
    }
}
