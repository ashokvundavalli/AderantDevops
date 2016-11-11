using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Aderant.Build.Tasks.WarningProcess {
    internal class BuildLogProcessor {
        public WarningComparison GetWarnings(Stream first, Stream second) {
            using (TextReader baselineText = new StreamReader(first, Encoding.UTF8)) {
                using (TextReader currentLogText = new StreamReader(second, Encoding.UTF8)) {
                    return GetWarnings(baselineText, currentLogText);
                }
            }
        }
        public WarningComparison GetWarnings(TextReader first, TextReader second) {
            var baselineWarnings = GetWarnings(first).ToList();
            var newerWarnings = GetWarnings(second).ToList();

            return new WarningComparison(baselineWarnings, newerWarnings);
        }

        public string CreateWarningReport(WarningComparison report, string url) {
            var reportBuilder = new WarningReportBuilder();
            return reportBuilder.CreateReport(report, url);
        }

        private static IEnumerable<WarningEntry> GetWarnings(TextReader reader) {
            var parser = new BuildLogParser();
            return parser.GetWarningEntries(reader);
        }
    }
}