using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using Aderant.Build.DependencyAnalyzer;
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
                if (item.EvaluatedInclude.EndsWith(".tt", StringComparison.OrdinalIgnoreCase)) {
                    string filePath = item.GetMetadataValue("FullPath");

                    var generator = item.GetMetadataValue("Generator");

                    if (string.Equals(generator, "TextTemplatingFileGenerator", StringComparison.OrdinalIgnoreCase)) {
                        AnalyzeTemplate(filePath, project, unresolvedReferences, new List<string>());
                    }
                }
            }

            return unresolvedReferences;
        }

        private void AnalyzeTemplate(string filePath, ConfiguredProject project, List<IUnresolvedAssemblyReference> unresolvedReferences, List<string> seenTemplates) {
            using (Stream openFile = fileSystem.OpenFile(filePath)) {
                using (var reader = new StreamReader(openFile)) {

                    var analyzer = new TextTemplateAnalyzer();
                    TextTemplateAnalysisResult result = analyzer.Analyze(reader, project.FullPath);

                    CreateReferences(result, unresolvedReferences);

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
                UnresolvedAssemblyReferenceMoniker moniker;

                if (assemblyReference.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                    moniker = new UnresolvedAssemblyReferenceMoniker(null, assemblyReference);
                } else {
                    moniker = new UnresolvedAssemblyReferenceMoniker(new AssemblyName(assemblyReference), null);
                }

                unresolvedReferences.Add(CreateUnresolvedReference(moniker));
            }
        }
    }
}
