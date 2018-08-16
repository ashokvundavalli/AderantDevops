using System;
using System.Diagnostics;

namespace Aderant.Build.VersionControl.Model {
    [DebuggerDisplay("Path: {Path} Status: {Status}")]
    [Serializable]
    public class SourceChange : ISourceChange {

        public SourceChange(string workingDirectory, string relativePath, FileStatus status) {
            Path = relativePath;
            FullPath = CleanPath(workingDirectory, relativePath);
            Status = status;
        }

        public string FullPath { get; }

        public string Path { get; }
        public FileStatus Status { get; }

        private static string CleanPath(string workingDirectory, string relativePath) {
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(workingDirectory, relativePath));
        }
    }
}
