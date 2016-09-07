using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aderant.Build.Tasks.WarningProcess {
    internal class WarningReportBuilder {
        // Two spaces for a markdown linebreak
        const string MarkdownLinebreak = "  ";
        public string CreateReport(WarningComparison warningComparison, string referenceUrl) {
            List<WarningEntry> difference = warningComparison.GetDifference().ToList();

            var hasTestDeploymentIssue = difference.Any(entry => entry.IsTestDeploymentIssue);
            var hasUnresolvedReference = difference.Any(entry => entry.IsUnresolvedReference);

            StringBuilder sb = new StringBuilder();

            if (difference.Any()) {
                sb.AppendLine(string.Format("The following warnings are in this build but not in the [reference build]({0}).", referenceUrl ?? string.Empty));
                sb.AppendLine();

                ExplainDifference(sb, difference, warningComparison);
                sb.AppendLine();

                foreach (var entry in difference) {
                    sb.AppendLine(CreateLine(entry.Message));
                    sb.AppendLine();
                }
                
                if (hasTestDeploymentIssue) {
                    sb.AppendLine("This build has test deployment warnings. Duplicate warnings will not appear in the summary.");
                    sb.AppendLine();
                }

                sb.AppendLine();
                if (hasUnresolvedReference) {
                    sb.AppendLine("This build has unresolved assembly references. Duplicate warnings for the same reference will not appear in the summary.");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        internal static string CreateLine(string message) {
            if (string.IsNullOrWhiteSpace(message)) {
                return string.Empty;
            }
            // example
            // Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23): Warning CS1591: Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'

            // We don't want ## as it will get interpreted by markdown as a title
            string line = message.Replace("##[warning]", String.Empty);

            // Some MSBuild warning messages have source: warning: reason format
            // so we'll just split on the first string
            string[] parts = line.Split(':');
            string file = parts[0];

            if (parts.Length == 1) {
                return message;
            }

            string warningMessage = string.Empty;
            if (parts.Length > 1) {
                warningMessage = string.Join("", parts.Skip(1)).Trim();
            }
          
            return file + MarkdownLinebreak + Environment.NewLine + warningMessage;
        }

        private void ExplainDifference(StringBuilder sb, List<WarningEntry> difference, WarningComparison comparison) {
            if (difference.Count != (comparison.Target.Count() - comparison.Source.Count())) {
                sb.AppendLine("There is no warning origin correlation performed during the build. This may cause more warnings to be present below than expected. " +
                              "This is because a small code change can alter the origin of one more warnings. For example adding a new property to a type can alter the source lines for existing warnings.");
            }
        }
    }
}