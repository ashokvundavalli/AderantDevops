using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Packaging;
using Aderant.Build.VersionControl;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {

    public class WriteBuildStateFile : BuildOperationContextTask {

        public override bool Execute() {
            if (Context.BuildMetadata?.BuildSourcesDirectory != null) {
                string sourcesDirectory = Context.BuildMetadata.BuildSourcesDirectory;

                var writer = new BuildStateWriter();
                writer.WriteStateFile(
                    sourcesDirectory,
                    Context.GetProjectOutputs(),
                    Context.GetArtifacts(),
                    Context.SourceTreeMetadata,
                    Context.BuildMetadata,
                    Path.Combine(Context.GetDropLocation(), BuildStateWriter.DefaultFileName));
            }

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
        /// <param name="sourcesDirectory">The ultimate root directory of the source</param>
        /// <param name="outputs">The outputs the build produced</param>
        /// <param name="artifacts">The artifacts this build produced</param>
        /// <param name="metadata">A description of the source tree</param>
        /// <param name="buildMetadata">A description of the current build environment</param>
        /// <param name="path">The path to write the file to</param>
        public void WriteStateFile(string sourcesDirectory, IDictionary<string, ProjectOutputs> outputs, IDictionary<string, string[]> artifacts, SourceTreeMetadata metadata, BuildMetadata buildMetadata, string path) {
            ErrorUtilities.IsNotNull(sourcesDirectory, nameof(sourcesDirectory));

            string bucketId = null;
            if (metadata != null) {
                var treeSha = metadata.GetBucket(BucketId.Current);
                bucketId = treeSha.Id;
            }

            var stateFile = new BuildStateFile {
                TreeSha = bucketId
            };

            if (outputs != null) {
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
        }

        private static string TrimSourcesDirectory(string sourcesDirectory, string path) {
            if (path.StartsWith(sourcesDirectory, StringComparison.OrdinalIgnoreCase)) {
                return path.Remove(0, sourcesDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar)
                    .TrimStart(Path.AltDirectorySeparatorChar);
            }

            return path;
        }
    }

    [Serializable]
    [DataContract]
    public sealed class BuildStateFile : StateFileBase {

        [DataMember(Name = nameof(BuildId), EmitDefaultValue = false)]
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

        [DataMember(EmitDefaultValue = false, Name = nameof(Outputs))]
        internal IDictionary<string, ProjectOutputs> Outputs { get; set; }

        [DataMember(EmitDefaultValue = false, Name = nameof(Artifacts))]
        internal IDictionary<string, string[]> Artifacts { get; set; }
    }

    [Serializable]
    [DataContract]
    public class StateFileBase {

        private const byte CurrentSerializationVersion = 1;

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
        public string DependencyManifestFile { get; set; }

        [Required]
        public string ArtifactDirectory { get; set; }

        public ITaskItem[] ArtifactIds { get; set; }

        public bool Flatten { get; set; }

        public override bool Execute() {
            if (ArtifactIds != null) {
                var document = XDocument.Load(DependencyManifestFile);
                var manifest = DependencyManifest.Load(document);

                var service = new ArtifactService();
                var result = service.Resolve(Context, manifest, ArtifactIds.Select(s => s.ItemSpec));
                service.Retrieve(result, ArtifactDirectory, Flatten);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
