using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Aderant.Build.Packaging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.Services;
using Aderant.Build.Tasks;
using Aderant.Build.VersionControl;

namespace Aderant.Build {

    [Serializable]
    public class BuildOperationContext {
        private ArtifactCollection artifacts;
        private BuildMetadata buildMetadata;

        private string buildScriptsDirectory;
        private BuildStateMetadata buildStateMetadata;
        private bool isDesktopBuild = true;

        // For deterministic hashing it is better if this is sorted
        private ProjectOutputCollection outputs;

        private string primaryDropLocation;
        private string pullRequestDropLocation;

        [NonSerialized]
        private IContextualServiceProvider serviceProvider;

        private SourceTreeMetadata sourceTreeMetadata;
        private List<BuildStateFile> stateFile;

        private BuildSwitches switches = default(BuildSwitches);

        public BuildOperationContext() {
            Configuration = new Dictionary<object, object>();
            VariableBags = new SortedDictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            TaskIndex = -1;
            Variables = new Dictionary<string, string>();
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

        public DirectoryInfo BuildRoot { get; set; }

        public string BuildSystemDirectory { get; set; }

        public bool IsDesktopBuild {
            get { return isDesktopBuild; }
            set { isDesktopBuild = value; }
        }

        public IDictionary Configuration { get; set; }

        public FileInfo ConfigurationPath { get; set; }

        public DirectoryInfo DownloadRoot { get; set; }

        public string Environment { get; set; }

        public DirectoryInfo OutputDirectory { get; set; }

        public string PipelineName { get; set; }

        public bool Publish { get; set; }

        public DateTime StartedAt { get; set; }

        public string TaskName { get; set; }

        public int TaskIndex { get; set; }

        public IDictionary<string, IDictionary<string, string>> VariableBags { get; private set; }

        public DirectoryInfo Temp { get; set; }

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

        /// <summary>
        /// The state file this build is using (if any).
        /// This indicates if we are reusing an existing build.
        /// </summary>
        public List<BuildStateFile> StateFile {
            get { return stateFile; }
            set { stateFile = value; }
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

        public DependencyRelationshipProcessing GetRelationshipProcessingMode() {
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

            if (StateFile != null) {
                return ChangesToConsider.PendingChanges;
            }

            return mode;
        }

        public void PutVariable(string id, string key, string value) {
            var bags = VariableBags;

            IDictionary<string, string> bag;
            if (!bags.TryGetValue(id, out bag)) {
                bag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bags[id] = bag;
            }

            bag[key] = value;
        }

        public string GetVariable(string id, string key) {
            ErrorUtilities.IsNotNull(id, nameof(id));
            ErrorUtilities.IsNotNull(key, nameof(key));

            var bags = VariableBags;

            IDictionary<string, string> bag;
            if (bags.TryGetValue(id, out bag)) {
                string value;
                bag.TryGetValue(key, out value);

                return value;
            }

            return null;
        }

        public string GetDropLocation(string publisherName) {
            return BucketPathBuilder.BuildDropLocation(publisherName, this);
        }

        public void RecordProjectOutputs(string sourcesDirectory, string projectFile, string[] projectOutputs, string outputPath, string intermediateDirectory) {
            ErrorUtilities.IsNotNull(sourcesDirectory, nameof(sourcesDirectory));

            if (sourcesDirectory != null && projectFile.StartsWith(sourcesDirectory, StringComparison.OrdinalIgnoreCase)) {
                projectFile = projectFile.Substring(sourcesDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar)
                    .TrimStart(Path.AltDirectorySeparatorChar);
            }

            if (outputs == null) {
                outputs = new ProjectOutputCollection();
            }

            if (!outputs.ContainsKey(projectFile)) {
                outputs[projectFile] = new ProjectOutputs {
                    FilesWritten = RemoveIntermediateObjects(projectOutputs, intermediateDirectory),
                    OutputPath = outputPath,
                    Origin = "ThisBuild",
                    Directory = ProjectOutputs.GetDirectory(projectFile)
                };
            } else {
                ThrowDoubleWrite();
            }
        }

        private static void ThrowDoubleWrite() {
            throw new InvalidOperationException("Possible double write detected");
        }

        private static string[] RemoveIntermediateObjects(string[] projectOutputs, string path) {
            return projectOutputs
                .Where(item => item.IndexOf(path, StringComparison.OrdinalIgnoreCase) == -1)
                .OrderBy(filePath => filePath)
                .ToArray();
        }

        /// <summary>
        /// Returns the outputs for all projects seen by the build.
        /// Keyed by project file.
        /// </summary>
        internal ProjectOutputCollection GetProjectOutputs() {
            return outputs;
        }

        /// <summary>
        /// Returns the artifacts for a given publisher.
        /// Keyed by publisher.
        /// </summary>
        internal ArtifactCollection GetArtifacts() {
            return artifacts;
        }

        internal void RecordArtifact(string publisherName, string artifactId, ICollection<ArtifactItem> files) {
            if (artifacts == null) {
                artifacts = new ArtifactCollection();
            }

            ICollection<ArtifactManifest> manifests;

            if (!artifacts.TryGetValue(publisherName, out manifests)) {
                manifests = new List<ArtifactManifest>();
                artifacts[publisherName] = manifests;
            }

            manifests.Add(
                new ArtifactManifest {
                    Id = artifactId,
                    Files = files,
                });

            //manifests.Sort((x, y) => string.Compare(x.Id, y.Id, StringComparison.OrdinalIgnoreCase));
        }

        public BuildStateFile GetStateFile(string key) {
            foreach (var file in StateFile) {
                if (string.Equals(file.BucketId.Tag, key, StringComparison.OrdinalIgnoreCase)) {
                    return file;
                }
            }

            return null;
        }
    }

    [Serializable]
    [DataContract]
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
    [DataContract]
    internal class ProjectOutputCollection : SortedDictionary<string, ProjectOutputs> {

        public ProjectOutputCollection()
            : base(StringComparer.OrdinalIgnoreCase) {
        }
        
        public ProjectOutputCollection GetProjectsForTag(string tag) {
            var items = this.Where(m => string.Equals(m.Value.Directory, tag, StringComparison.OrdinalIgnoreCase));

            var collection = new ProjectOutputCollection();
            foreach (var item in items) {
                collection.Add(item.Key, item.Value);
            }

            return collection;
        }
    }

    [Serializable]
    [DataContract]
    internal class ArtifactManifest {

        [DataMember(Order = 0)]
        public string Id { get; set; }

        [DataMember(Order = 1)]
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
    internal class ProjectOutputs {

        [DataMember]
        public string[] FilesWritten { get; set; }

        [DataMember]
        public string OutputPath { get; set; }

        [DataMember]
        public string Origin { get; set; }

        [DataMember]
        public string Directory { get; set; }

        public static string GetDirectory(string projectFile) {
            return projectFile.Split(Path.DirectorySeparatorChar)[0];
        }
    }

    [Serializable]
    public sealed class SourceTreeMetadata {

        public string CommonAncestor { get; set; }
        public IReadOnlyCollection<BucketId> BucketIds { get; set; }
        public IReadOnlyCollection<SourceChange> Changes { get; set; }
        public string NewCommitDisplay { get; set; }
        public string OldCommitDisplay { get; set; }

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
    public struct BuildSwitches {

        public bool PendingChanges { get; set; }
        public bool Downstream { get; set; }
        public bool Transitive { get; set; }
        public bool Everything { get; set; }
        public bool Clean { get; set; }
        public bool Release { get; set; }
        public bool DryRun { get; set; }
        public bool Resume { get; set; }

        [CreateProperty]
        public bool SkipCompile { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class CreatePropertyAttribute : Attribute {
    }

}
