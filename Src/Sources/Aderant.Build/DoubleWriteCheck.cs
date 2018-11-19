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

                if (CheckFileSize) {
                    var fileData = group.Select(s => createFileInfo(s.Location));
                    fail = fileData.Select(s => s.Length).Distinct().Count() > 1;
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
    }
}