using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aderant.Build.Packaging;

namespace Aderant.Build {
    /// <summary>
    /// Fails if any double writes are detected.
    /// Confirms that two or more files do not write to the same destination. This ensures deterministic behavior.
    /// </summary>
    internal static class DoubleWriteCheck {

        public static void CheckForDoubleWrites(IEnumerable<PathSpec> filePathList) {
            var duplicates = filePathList
                .GroupBy(g => g.Destination, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            StringBuilder sb = null;

            foreach (var group in duplicates) {
                foreach (var path in group) {
                    if (sb == null) {
                        sb = new StringBuilder();
                    }
                    sb.AppendFormat("Double write: {0} -> {1}.", path.Location, path.Destination);
                    sb.AppendLine();
                }
            }

            if (sb != null) {
                string errorText = sb.ToString();
                throw new InvalidOperationException(errorText);
            }
        }
    }
}
