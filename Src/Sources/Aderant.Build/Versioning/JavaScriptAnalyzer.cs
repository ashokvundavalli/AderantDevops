using System;
using System.IO;
using System.Text.RegularExpressions;
using Aderant.Build.Packaging;

namespace Aderant.Build.Versioning {
    internal class JavaScriptAnalyzer : IVersionAnalyzer<string> {
        private static Regex pattern = new Regex(@"(v\s?)(\d+\.)?(\d+\.)?(\d+)", RegexOptions.IgnoreCase);

        public FileVersionDescriptor GetVersion(string text) {
            Match m = pattern.Match(text);
            string version = m.Value;

            if (!string.IsNullOrEmpty(version)) {
                version = version
                    .TrimStart(new char[] {'v', 'V'})
                    .TrimStart(null);
            }

            return new FileVersionDescriptor(version, null);
        }

        public bool CanAnalyze(FileInfo file) {
            if (file.Extension.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            return false;
        }
    }
}