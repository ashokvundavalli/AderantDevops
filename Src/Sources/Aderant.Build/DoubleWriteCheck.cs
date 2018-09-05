using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aderant.Build {
    /// <summary>
    /// Fails if any double writes are detected.
    /// Confirms that two or more files do not write to the same destination. This ensures deterministic behavior.
    /// </summary>
    internal static class DoubleWriteCheck {

        public static void CheckForDoubleWrites(IEnumerable<string> filePathList) {
            IEnumerable<IGrouping<string, string>> duplicates = filePathList
                .GroupBy(g => g, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            StringBuilder sb = null;

            foreach (var group in duplicates) {
                foreach (var g in group) {

                    if (sb == null) {
                        sb = new StringBuilder();
                        sb.AppendFormat("Double write detected for file path: {0}.", g);
                    }
                }
            }

            if (sb != null) {
                sb.Append(" ").AppendLine("The full write list is: ");
                foreach (var s in filePathList) {
                    sb.AppendLine(s);
                }

                string errorText = sb.ToString();

                throw new InvalidOperationException(errorText);
            }
        }
    }
}
