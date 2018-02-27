using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aderant.Build;

namespace Aderant.BuildTime.Tasks.Sequencer {
    internal class TextTemplateAnalyzer {
        private readonly IFileSystem2 fileSystem;
        private List<string> excludedPatterns = new List<string>();
        static Regex nameMatch = new Regex(@"name=""([^""]*)\""", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public TextTemplateAnalyzer(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
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

        public List<TextTemplateAssemblyInfo> GetDependencies(string solutionRoot) {
            var files = fileSystem.GetFiles(solutionRoot, "*.tt*", true, true);

            var templateList = files.Where(f => !excludedPatterns.Any(s => f.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0));

            List<TextTemplateAssemblyInfo> dependencies = new List<TextTemplateAssemblyInfo>();

            foreach (var file in templateList) {
                try {
                    using (var fs = fileSystem.OpenFile(file)) {
                        var reader = new StreamReader(fs);

                        var template = new TextTemplateAssemblyInfo(file);
                        dependencies.Add(template);

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
                    }
                } catch {
                    continue;
                }
            }

            return dependencies;
        }
    }

    internal class TextTemplateAssemblyInfo {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextTemplateAssemblyInfo"/> class.
        /// </summary>
        /// <param name="templateFile">The template file.</param>
        public TextTemplateAssemblyInfo(string templateFile) {
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