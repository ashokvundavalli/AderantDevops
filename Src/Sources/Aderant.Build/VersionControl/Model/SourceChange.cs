using System.Diagnostics;
using System.Runtime.Serialization;
using ProtoBuf;

namespace Aderant.Build.VersionControl.Model {
    [DebuggerDisplay("Path: {Path} Status: {Status}")]
    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SourceChange : ISourceChange {
        private SourceChange() {
        }

        public SourceChange(string workingDirectory, string relativePath, FileStatus status) {
            Path = relativePath;
            FullPath = CleanPath(workingDirectory, relativePath);
            Status = status;
        }

        [DataMember]
        public string FullPath { get; }

        [DataMember]
        public string Path { get; }

        [DataMember]
        public FileStatus Status { get; }

        private static string CleanPath(string workingDirectory, string relativePath) {
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(workingDirectory, relativePath));
        }
    }
}
