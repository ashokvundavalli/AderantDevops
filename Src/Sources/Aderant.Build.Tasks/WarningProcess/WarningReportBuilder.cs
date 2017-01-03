using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        // compiler warning messages have source: warning: reason format
        static Regex warningRegex = new Regex("(?i)warning [A-Z]+[0-9]+:");
        private static char[] trimChars = new[] { ' ', ':' };

        internal static string CreateLine(string line) {
            if (string.IsNullOrWhiteSpace(line)) {
                return string.Empty;
            }
            // example
            // Src\Aderant.Database.Build\StoredProcedureDelegateCompiler.cs(13, 23): Warning CS1591: Missing XML comment for publicly visible type or member 'StoredProcedureDelegateCompiler.foo'

            // We don't want ## as it will get interpreted by markdown as a title
            string cleanline = line.Replace("##[warning]", String.Empty);

            var result = warningRegex.Match(cleanline);
            if (result.Success) {

                var splitPosition = result.Index;

                var warningText = cleanline.Substring(splitPosition);
                var file = cleanline.Substring(0, splitPosition);
       
                file = file.TrimEnd(trimChars);

                return $"{file}{MarkdownLinebreak}{Environment.NewLine}{warningText}";
            } 

            return cleanline;
        }

        private void ExplainDifference(StringBuilder sb, List<WarningEntry> difference, WarningComparison comparison) {
            if (difference.Count != (comparison.Target.Count() - comparison.Source.Count())) {
                sb.AppendLine("There is no warning origin correlation performed during the build. This may cause more warnings to be present below than expected. " +
                              "This is because a small code change can alter the origin of one or more warnings. For example adding a new property to a type can alter the source lines for existing warnings.");

                sb.AppendLine();

                sb.AppendLine("If a project uses FxCop for static analysis then there are two warnings logged for every warning. " +
                              "This means the warning count reported in Visual Studio will differ from that on the command line. " +
                              "The command line count is used for the warning calculation");
            }
        }
    }
}