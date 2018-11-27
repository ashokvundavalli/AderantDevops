using System;
using System.IO;
using System.Runtime.Serialization;
using Aderant.Build.TeamFoundation;
using Aderant.Build.Utilities;
using ProtoBuf;

namespace Aderant.Build.Packaging {

    /// <summary>
    /// A build artifact is a network accessible directory.
    /// </summary>
    /// <remarks>
    /// The directory path is registered with TFS so it's garbage collection routine can purge obsoleted artifacts.
    /// So you would think that TFS would take the path verbatim and just store that away.
    /// But no, it takes the UNC path you give it and then when the garbage collection occurs it appends the artifact name as a
    /// folder to that original path as the final path to delete.
    /// </remarks>
    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    internal class BuildArtifact {

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string SourcePath { get; set; }

        [DataMember]
        public string StoragePath { get; set; }

        [DataMember]
        public VsoBuildArtifactType Type { get; set; }

        [DataMember]
        public bool IsAutomaticallyGenerated { get; set; }

        [DataMember]
        public bool IsInternalDevelopmentPackage { get; set; }

        [DataMember]
        public bool SendToArtifactCache { get; set; }

        [IgnoreDataMember]
        [ProtoIgnore]
        public bool IsTestPackage {
            get {
                if (Name.IndexOf("IntegrationTest", StringComparison.OrdinalIgnoreCase) >= 0) {
                    return true;
                }

                if (Name.EndsWith(".tests", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Creates a path to the artifact which TFS can use
        /// </summary>
        public string ComputeVsoPath() {
            if (string.IsNullOrWhiteSpace(StoragePath)) {
                throw new InvalidOperationException("Storage path not set for artifact:" + Name);
            }

            var pos = StoragePath.LastIndexOf(Name, StringComparison.OrdinalIgnoreCase);
            if (pos >= 0) {
                return StoragePath.Remove(pos)
                    .TrimEnd(Path.DirectorySeparatorChar)
                    .TrimEnd(Path.AltDirectorySeparatorChar);
            }

            return StoragePath;
        }

        public string CreateStoragePath(string sourcePath, string destinationPath) {
            if (SourcePath.Contains("..")) {
                throw new InvalidOperationException("Unsupported path. The path cannot contain directory operators such as [..]. The path was: " + SourcePath);
            }

            sourcePath = Path.GetFullPath(sourcePath);
            destinationPath = Path.GetFullPath(destinationPath);

            sourcePath = sourcePath.TrimEnd(Path.DirectorySeparatorChar);
            destinationPath = destinationPath.TrimEnd(Path.DirectorySeparatorChar);

            int pos = SourcePath.IndexOf(sourcePath, StringComparison.OrdinalIgnoreCase);
            if (pos >= 0) {
                var newPath = SourcePath.Replace(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase);

                StoragePath = newPath;

                return newPath;
            }

            return null;
        }
    }

}
