using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Aderant.Build.Packaging;
using Aderant.Build.VersionControl;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    public class WriteBuildStateFile : BuildOperationContextTask {

        protected override bool UpdateContextOnCompletion { get; set; } = true;

        public override bool ExecuteTask() {
            var writer = new BuildStateWriter();
            writer.WriteStateFiles(Context);

            //var path = writer.WriteStateFile(
            //    Context.StateFile,
            //    Context.GetProjectOutputs(),
            //    Context.GetArtifacts(),
            //    Context.SourceTreeMetadata,
            //    Context.BuildMetadata,
            //    Path.Combine(Context.GetDropLocation(null), BuildStateWriter.DefaultFileName));

            return !Log.HasLoggedErrors;
        }
    }

    internal class BuildStateWriter {
        private readonly IFileSystem fileSystem;

        public BuildStateWriter()
            : this(new PhysicalFileSystem()) {
        }

        internal BuildStateWriter(IFileSystem fileSystem) {
            this.fileSystem = fileSystem;
        }

        public static string DefaultFileName { get; private set; } = "buildstate.metadata";

        /// <summary>
        /// Serializes the build state file
        /// </summary>
        /// <param name="selectedStateFile">The state file selected to build with.</param>
        /// <param name="directorySha">The SHA of the directory for which the outputs are associated</param>
        /// <param name="outputs">The outputs the build produced</param>
        /// <param name="artifacts">The artifacts this build produced</param>
        /// <param name="metadata">A description of the source tree</param>
        /// <param name="buildMetadata">A description of the current build environment</param>
        /// <param name="path">The path to write the file to</param>
        public string WriteStateFile(
            BuildStateFile selectedStateFile,
            BucketId directorySha,
            ProjectOutputCollection outputs,
            IDictionary<string, ICollection<ArtifactManifest>> artifacts,
            SourceTreeMetadata metadata,
            BuildMetadata buildMetadata,
            string path) {

            string treeShaValue = null;
            if (metadata != null) {
                var treeSha = metadata.GetBucket(BucketId.Current);
                treeShaValue = treeSha.Id;
            }

            var stateFile = new BuildStateFile {
                TreeSha = treeShaValue,
                BucketId = directorySha
            };

            if (selectedStateFile != null) {
                stateFile.ParentId = selectedStateFile.Id;
                stateFile.ParentBuildId = selectedStateFile.BuildId;
                stateFile.ParentTreeSha = selectedStateFile.TreeSha;
            }

            if (outputs != null) {
                if (selectedStateFile != null) {
                    MergeExistingOutputs(selectedStateFile.BuildId, selectedStateFile.Outputs, outputs);
                }

                stateFile.Outputs = outputs;
            }

            if (artifacts != null) {
                stateFile.Artifacts = artifacts;
            }

            if (buildMetadata != null) {
                stateFile.BuildId = buildMetadata.BuildId.ToString(CultureInfo.InvariantCulture);

                if (buildMetadata.IsPullRequest) {
                    stateFile.PullRequestInfo = buildMetadata.PullRequest;
                } else {
                    stateFile.ScmBranch = buildMetadata.ScmBranch;
                    stateFile.ScmCommitId = buildMetadata.ScmCommitId;
                }
            }

            fileSystem.AddFile(path, stream => stateFile.Serialize(stream));

            return path;
        }

        private void MergeExistingOutputs(string buildId, IDictionary<string, ProjectOutputs> oldOutput, IDictionary<string, ProjectOutputs> newOutput) {
            // TODO: We don't want to keep re-adding deleted stuff... how to handle?
            foreach (var projectOutputs in oldOutput) {
                if (!newOutput.ContainsKey(projectOutputs.Key)) {
                    var outputs = projectOutputs.Value;
                    outputs.Origin = buildId;

                    newOutput[projectOutputs.Key] = outputs;
                }
            }
        }

        public void WriteStateFiles(BuildOperationContext context) {
            IReadOnlyCollection<BucketId> buckets = context.SourceTreeMetadata.GetBuckets();

            foreach (var bucket in buckets) {
                var tag = bucket.Tag;

                var outputs = context.GetProjectOutputs();
                var projectOutputCollection = outputs.GetProjectsForTag(tag);

                var artifacts = context.GetArtifacts();
                var artifactCollection = artifacts.GetArtifactsForTag(tag);

                var dropLocation = Path.Combine(context.GetDropLocation(tag), DefaultFileName);
                WriteStateFile(null, bucket, projectOutputCollection, artifactCollection, context.SourceTreeMetadata, context.BuildMetadata, dropLocation);
            }
        }
    }

    [Serializable]
    [DataContract]
    internal class ProjectKey {
        public string Path { get; set; }
    }

    [Serializable]
    [DataContract]
    public sealed class BuildStateFile : StateFileBase {

        [DataMember(EmitDefaultValue = false)]
        public BucketId BucketId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DataMember(EmitDefaultValue = false)]
        public string BuildId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string TreeSha { get; set; }

        [IgnoreDataMember]
        internal string DropLocation { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public PullRequestInfo PullRequestInfo { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ScmBranch { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ScmCommitId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        internal IDictionary<string, ProjectOutputs> Outputs { get; set; }

        [DataMember(EmitDefaultValue = false)]
        internal IDictionary<string, ICollection<ArtifactManifest>> Artifacts { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ParentBuildId { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ParentTreeSha { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public Guid ParentId { get; set; }
    }

    [Serializable]
    [DataContract]
    public class StateFileBase {

        private const byte CurrentSerializationVersion = 2;

        // Version this instance is serialized with.
        [DataMember]
        internal byte serializedVersion = CurrentSerializationVersion;

        internal T DeserializeCache<T>(Stream stream) where T : StateFileBase {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(
                typeof(T),
                new DataContractJsonSerializerSettings {
                    UseSimpleDictionaryFormat = true
                });

            object readObject = ser.ReadObject(stream);

            T stateFile = readObject as T;

            if (stateFile != null && stateFile.serializedVersion != serializedVersion) {
                return null;
            }

            return stateFile;
        }

        /// <summary>
        /// Writes the contents of this object out.
        /// </summary>
        /// <param name="stream"></param>
        internal virtual void Serialize(Stream stream) {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(
                GetType(),
                new DataContractJsonSerializerSettings {
                    UseSimpleDictionaryFormat = true
                });

            ser.WriteObject(stream, this);
        }
    }

    public sealed class RetrieveArtifacts : BuildOperationContextTask {

        [Required]
        public string SolutionRoot { get; set; }

        public string PublisherName { get; set; }

        public string WorkingDirectory { get; set; }

        protected override bool UpdateContextOnCompletion { get; set; } = false;

        public override bool ExecuteTask() {
            var service = new ArtifactService(Logger);
            service.Resolve(Context, PublisherName, SolutionRoot, WorkingDirectory);

            return !Log.HasLoggedErrors;
        }
    }

    public sealed class PrintBanner : Task {

        private static string header = "╔═════════════════════════════════════════════════════════════════════╗";
        private static string side = "║";
        private static string footer = "╚═════════════════════════════════════════════════════════════════════╝";

        public string Text { get; set; }

        public override bool Execute() {
            if (string.IsNullOrWhiteSpace(Text)) {
                return true;
            }

            var center = header.Length / 2 + Text.Length / 2;

            string text = Text.PadLeft(center).PadRight(header.Length - 2);

            StringBuilder sb = new StringBuilder(header);
            sb.AppendLine();
            sb.Append(side);
            sb.Append(text);
            sb.Append(side);
            sb.AppendLine();
            sb.Append(footer);

            Log.LogMessage(sb.ToString());

            return true;
        }
    }

}
