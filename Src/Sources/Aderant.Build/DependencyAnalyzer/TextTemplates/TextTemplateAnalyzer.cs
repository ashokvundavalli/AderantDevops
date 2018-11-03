using System;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build.DependencyAnalyzer.TextTemplates {
    internal class TextTemplateAnalyzer {
        private MacroExpander expander;

        public TextTemplateAnalyzer() {
        }

        public TextTemplateAnalysisResult Analyze(TextReader reader, string projectDirectory) {
            expander = new MacroExpander();
            expander.ProjectDir = projectDirectory;

            var template = new TextTemplateAnalysisResult();

            var tokenizer = new Tokenizer(null, reader.ReadToEnd());

            //      AnalyzerState state = AnalyzerState.ExpectingAttribute;

            bool isAssemblyElement = false;
            bool isIncludeElement = false;

            while (tokenizer.Advance()) {
                if (!string.IsNullOrEmpty(tokenizer.Value)) {
                    string tokenizerValue = tokenizer.Value;

                    if (isAssemblyElement && tokenizer.State == TokenizerState.DirectiveValue) {
                        ParseAssembly(template, tokenizerValue);
                        
                        isAssemblyElement = false;
                        continue;
                    }

                    if (isIncludeElement && tokenizer.State == TokenizerState.DirectiveValue) {
                        ParseInclude(template, tokenizerValue);
                        isIncludeElement = false;
                        continue;
                    }

                    if (tokenizer.State == TokenizerState.DirectiveName) {
                        if (string.Equals(tokenizerValue, "assembly", StringComparison.OrdinalIgnoreCase)) {
                            isAssemblyElement = true;
                        } else if (string.Equals(tokenizerValue, "include", StringComparison.InvariantCultureIgnoreCase)) {
                            isIncludeElement = true;
                        }
                    }
                }
            }

            return template;
        }

        private void ParseInclude(TextTemplateAnalysisResult template, string tokenizerValue) {
            var value = expander.Expand(tokenizerValue);
            template.Includes.Add(value);
        }

        private void ParseAssembly(TextTemplateAnalysisResult template, string tokenizerValue) {
            var value = expander.Expand(tokenizerValue);
            template.AssemblyReferences.Add(Path.GetFileName(value));
        }
    }

    internal class TextTemplateAnalysisResult {
        public TextTemplateAnalysisResult() {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TextTemplateAnalysisResult" /> class.
        /// </summary>
        /// <param name="templateFile">The template file.</param>
        public TextTemplateAnalysisResult(string templateFile) {
            TemplateFile = templateFile;
        }

        /// <summary>
        /// Gets the assemblies this template requires.
        /// </summary>
        /// <value>The assembly.</value>
        public ICollection<string> AssemblyReferences { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the template file path.
        /// </summary>
        /// <value>The template file.</value>
        public string TemplateFile { get; }

        public IList<string> Includes { get; private set; } = new List<string>();
    }
}
