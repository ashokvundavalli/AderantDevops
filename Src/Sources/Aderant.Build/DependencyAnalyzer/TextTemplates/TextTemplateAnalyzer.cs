using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>
        /// Add an exclusion pattern.
        /// </summary>
        /// <param name="pattern">The pattern to exclude</param>
        public void AddExclusionPattern(string pattern) {
            if (!excludedPatterns.Contains(pattern)) {
                excludedPatterns.Add(pattern);
            }
        }

        public List<TextTemplateAnalysisResult> GetDependencies(string solutionRoot) {
            IEnumerable<string> files = fileSystem.GetFiles(solutionRoot, "*.tt*", true);

            IEnumerable<string> templateList = files.Where(f => !excludedPatterns.Any(s => f.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0));

            List<TextTemplateAnalysisResult> dependencies = new List<TextTemplateAnalysisResult>();

            foreach (var file in templateList) {
                try {
                    using (var fs = fileSystem.OpenFile(file)) {
                        var reader = new StreamReader(fs);

                        var template = new TextTemplateAnalysisResult(file);

                        string text;
                        while ((text = reader.ReadLine()) != null) {
                            var line = text.TrimStart();

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

                                    var shortName = assemblyNameValue.Split(',')[0];

                                    if (shortName.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) || shortName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                                        shortName = Path.GetFileNameWithoutExtension(assemblyNameValue.Trim());
                                    } else {
                                        shortName = Path.GetFileName(shortName.Trim());
                                    }

                                    template.AssemblyReferences.Add(shortName);
                                }
                            }
                        }

                        dependencies.Add(template);
                    }
                } catch {
                    continue;
                }
            }

            return dependencies;
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
