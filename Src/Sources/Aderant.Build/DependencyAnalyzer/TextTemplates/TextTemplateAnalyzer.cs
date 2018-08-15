using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Aderant.Build.DependencyAnalyzer.TextTemplates;

namespace Aderant.Build.DependencyAnalyzer {
    internal class TextTemplateAnalyzer {
        static readonly Regex nameMatch = new Regex(@"name=""([^""]*)\""", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private readonly List<string> excludedPatterns = new List<string>();
        private readonly IFileSystem2 fileSystem;

        public TextTemplateAnalyzer(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        public TextTemplateAnalyzer() {
        }

        public TextTemplateAnalysisResult Analyze(TextReader reader, string projectDirectory) {
            var expander = new MacroExpander();
            expander.ProjectDir = projectDirectory;

            var template = new TextTemplateAnalysisResult();

            string text;
            while ((text = reader.ReadLine()) != null) {
                var line = text.TrimStart();

                line = expander.Expand(line);

                if (line.StartsWith("<#@")) {
                    if (line.IndexOf("ServiceDsl", StringComparison.OrdinalIgnoreCase) >= 0) {
                        template.IsServiceDslTemplate = true;
                    }

                    if (line.IndexOf("DomainModelDsl", StringComparison.OrdinalIgnoreCase) >= 0) {
                        template.IsDomainModelDslTemplate = true;
                    }

                    const string assemblyText = "assembly ";

                    if (line.IndexOf(assemblyText, StringComparison.OrdinalIgnoreCase) >= 0) {
                        Match match = nameMatch.Match(line);

                        Group matchGroup = match.Groups[1];
                        string assemblyNameValue = matchGroup.Value;
                        template.AssemblyReferences.Add(assemblyNameValue.Trim());
                    }
                }
            }

            return template;
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

        /// <summary>
        /// Gets or sets a value indicating whether this instance is a service DSL template.
        /// </summary>
        public bool IsServiceDslTemplate { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is a domain model DSL template.
        /// </summary>
        public bool IsDomainModelDslTemplate { get; internal set; }
    }
}
