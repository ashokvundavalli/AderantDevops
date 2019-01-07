using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using Aderant.Build.DependencyAnalyzer.TextTemplates;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {

    [Export(typeof(ITextTemplateReferencesServices))]
    [ExportMetadata("Scope", nameof(ProjectSystem.ConfiguredProject))]
    internal class TextTemplateReferencesServices : AssemblyReferencesService, ITextTemplateReferencesServices {
        private readonly IFileSystem fileSystem;

        [ImportingConstructor]
        public TextTemplateReferencesServices(IFileSystem fileSystem) {
            this.fileSystem = fileSystem;
        }

        public override IReadOnlyCollection<IUnresolvedAssemblyReference> GetUnresolvedReferences() {
            var project = this.ConfiguredProject;

            // Grab all item groups that usually include a .tt file
            // The compile group usually does not reference a .tt file as you cannot compile a text file
            List<ProjectItem> projectItems = new List<ProjectItem>();
            projectItems.AddRange(project.GetItems("Content"));
            projectItems.AddRange(project.GetItems("None"));

            var unresolvedReferences = new List<IUnresolvedAssemblyReference>();

            foreach (ProjectItem item in projectItems) {
                if (ShouldParseTemplate(item.EvaluatedInclude)) {
                    string filePath = item.GetMetadataValue("FullPath");

                    var generator = item.GetMetadataValue("Generator");

                    if (string.Equals(generator, "TextTemplatingFileGenerator", StringComparison.OrdinalIgnoreCase)) {
                        AnalyzeTemplate(filePath, project, unresolvedReferences, new List<string>());
                    }
                }
            }

            return UnresolvedReferences = unresolvedReferences;
        }

        protected override IAssemblyReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, IUnresolvedAssemblyReference unresolved, Dictionary<string, string> aliasMap) {
            IReadOnlyCollection<ConfiguredProject> projects = ConfiguredProject.Tree.LoadedConfiguredProjects;

            if (unresolved.IsForTextTemplate) {
                if (aliasMap != null) {
                    string projectPath;
                    if (aliasMap.TryGetValue(unresolved.Id, out projectPath)) {

                        var project = projects.FirstOrDefault(s => s.FullPath.IndexOf(projectPath, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (project != null) {
                            return project;
                        }
                    }
                }
            }

            return base.CreateResolvedReference(references, unresolved, aliasMap);
        }

        private bool ShouldParseTemplate(string itemEvaluatedInclude) {
            if (!string.IsNullOrWhiteSpace(itemEvaluatedInclude) && itemEvaluatedInclude.EndsWith(".tt", StringComparison.OrdinalIgnoreCase)) {
                // __ is a special prefix that the build will ignore even if this template may cause a circular reference
                var itemEvaluatedIncludeFileName = Path.GetFileName(itemEvaluatedInclude);

                if (!itemEvaluatedIncludeFileName.StartsWith("__")) {
                    return true;
                }
            }

            return false;
        }

        private void AnalyzeTemplate(string filePath, ConfiguredProject project, List<IUnresolvedAssemblyReference> unresolvedReferences, List<string> seenTemplates) {
            if (!fileSystem.FileExists(filePath)) {
                return;
            }

            using (Stream openFile = fileSystem.OpenFile(filePath)) {
                using (var reader = new StreamReader(openFile)) {

                    var analyzer = new TextTemplateAnalyzer();
                    TextTemplateAnalysisResult result = analyzer.Analyze(reader, project.FullPath);

                    CreateReferences(result, unresolvedReferences);

                    if (result.CustomProcessors != null) {
                        foreach (var processor in result.CustomProcessors) {
                            AddReference(unresolvedReferences, processor, null);
                        }
                    }

                    if (result.Includes != null) {
                        for (var i = 0; i < result.Includes.Count; i++) {
                            var include = result.Includes[i];

                            string path = CreatePathToInclude(filePath, include);

                            if (path != null && !seenTemplates.Contains(path)) {
                                AnalyzeTemplate(path, project, unresolvedReferences, seenTemplates);
                                seenTemplates.Add(path);
                            }
                        }
                    }
                }
            }
        }

        private string CreatePathToInclude(string currentTemplatePath, string include) {
            if (!fileSystem.FileExists(include)) {
                return Path.Combine(Path.GetDirectoryName(currentTemplatePath), include);
            }

            return include;
        }

        private void CreateReferences(TextTemplateAnalysisResult result, List<IUnresolvedAssemblyReference> unresolvedReferences) {
            foreach (var assemblyReference in result.AssemblyReferences) {
                string path = null;
                string name = assemblyReference;
                if (assemblyReference.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                    path = assemblyReference;
                    name = assemblyReference.Substring(0, assemblyReference.Length - 4);
                }

                AddReference(unresolvedReferences, name, path);
            }
        }

        private static char[] invalidChars = Path.GetInvalidFileNameChars().Union(new[] { '=' }).ToArray();

        private void AddReference(List<IUnresolvedAssemblyReference> unresolvedReferences, string name, string path) {
            foreach (var ch in name) {
                if (invalidChars.Contains(ch)) {
                    return;
                }
            }

            var moniker = new UnresolvedAssemblyReferenceMoniker(new AssemblyName(name), path) { IsFromTextTemplate = true };

            var unresolvedAssemblyReference = CreateUnresolvedReference(moniker);
            unresolvedReferences.Add(unresolvedAssemblyReference);
        }
    }
}
