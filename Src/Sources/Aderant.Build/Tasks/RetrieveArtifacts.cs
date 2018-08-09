using System;
using System.Collections.Generic;
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

        [Required]
        public ITaskItem[] ProjectsInBuild { get; set; }

        public override bool Execute() {
            if (Context.BuildMetadata?.BuildSourcesDirectory != null) {
                string sourcesDirectory = Context.BuildMetadata.BuildSourcesDirectory;

                var writer = new BuildStateWriter();
                writer.WriteStateFile(
                    sourcesDirectory,
                    ProjectsInBuild.Select(s => s.GetMetadata("FullPath")),
                    Context.SourceTreeMetadata,
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

        public void WriteStateFile(string sourcesDirectory, IEnumerable<string> projectFiles, SourceTreeMetadata metadata, string path) {
            ErrorUtilities.IsNotNull(sourcesDirectory, nameof(sourcesDirectory));

            projectFiles = projectFiles
                .Where(s => !string.Equals(Path.GetExtension(s), ".targets", StringComparison.OrdinalIgnoreCase))
                .Select(s => TrimSourcesDirectory(sourcesDirectory, s))
                .OrderBy(s => s);

            string bucketId = null;
            if (metadata != null) {
                var treeSha = metadata.GetBucket(BucketId.Current);
                bucketId = treeSha.Id;
            }

            var stateFile = new BuildStateFile {
                ProjectFiles = projectFiles.ToArray(),
                TreeSha = bucketId
            };

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

        [DataMember]
        public string[] ProjectFiles { get; set; }

        [DataMember]
        public string TreeSha { get; set; }

        [IgnoreDataMember]
        internal string DropLocation { get; set; }
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

    public class RetrieveArtifacts : BuildOperationContextTask {

        [Required]
        public string DependencyManifestFile { get; set; }

        [Required]
        public string ArtifactDirectory { get; set; }

        public ITaskItem[] ArtifactDefinitions { get; set; }

        public override bool Execute() {
            if (ArtifactDefinitions != null) {
                var artifacts = ArtifactPackageHelper.MaterializeArtifactPackages(ArtifactDefinitions, null, null);

                var document = XDocument.Load(DependencyManifestFile);
                var manifest = DependencyManifest.Load(document);

                var service = new ArtifactService();
                service.Resolve(Context, manifest, ArtifactDirectory, artifacts);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
