using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build.Packaging;

namespace Aderant.Build {
    /// <summary>
    /// Fails if any double writes are detected.
    /// Confirms that two or more files do not write to the same destination. This ensures deterministic behavior.
    /// </summary>
    internal class DoubleWriteCheck {
        private readonly Func<string, FileInfo> createFileInfo;

        public DoubleWriteCheck() : this (file => new FileInfo(file)) {
        }

        public DoubleWriteCheck(Func<string, FileInfo> createFileInfo) {
            this.createFileInfo = createFileInfo;
        }

        public bool CheckFileSize { get; set; }

        public void CheckForDoubleWrites(IEnumerable<PathSpec> filePathList) {
            var duplicates = filePathList
                .GroupBy(g => g.Destination, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            StringBuilder sb = null;

            foreach (var group in duplicates) {
                bool fail = true;

                if (createFileInfo != null && CheckFileSize) {
                    List<FileInfo> files = new List<FileInfo>();

                    foreach (PathSpec spec in group) {
                        // Path spec may contain a RecursiveDir expression so it does not represent a path we can access
                        if (!spec.Location.Contains("**")) {
                            try {
                                FileInfo info = createFileInfo(spec.Location);
                                files.Add(info);
                            } catch {
                                IgnoredDoubleWrites.Add(spec);
                            }
                        }
                    }

                    fail = files.Select(s => s.Length).Distinct().Count() > 1;

                    if (!fail) {
                        foreach (PathSpec spec in group.Select(s => s)) {
                            if (!IgnoredDoubleWrites.Contains(spec)) {
                                IgnoredDoubleWrites.Add(spec);
                            }
                        }
                    }
                }

                // If all files are the same length, assume they are the same file
                if (fail) {
                    foreach (var path in group) {
                        if (sb == null) {
                            sb = new StringBuilder();
                        }

                        sb.AppendFormat("Double write: {0} -> {1}.", path.Location, path.Destination);
                        sb.AppendLine();
                    }
                }
            }

            if (sb != null) {
                string errorText = sb.ToString();
                if (!string.IsNullOrWhiteSpace(errorText)) {
                    throw new InvalidOperationException(errorText);
                }
            }
        }

        public ICollection<PathSpec> IgnoredDoubleWrites { get; set; } = new List<PathSpec>();
    }
}