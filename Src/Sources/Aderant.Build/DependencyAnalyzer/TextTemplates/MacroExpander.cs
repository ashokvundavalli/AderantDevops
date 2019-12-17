using System;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build.DependencyAnalyzer.TextTemplates {
    internal class MacroExpander {

        private static Dictionary<string, Func<MacroExpander, string>> macros =
            new Dictionary<string, Func<MacroExpander, string>> {
                { "$(ProjectDir)", m => m.ProjectDir },
            };

        public string ProjectDir { get; set; }

        public string Expand(string source) {
            foreach (var macro in macros) {
                source = source.Replace(macro.Key, macro.Value(this));
            }

            return source;
        }
    }
}
