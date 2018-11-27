using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Aderant.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class ReadAssemblyInfo : Task {
        private TaskItem assemblyFileVersion;
        private TaskItem assemblyInformationalVersion;
        private TaskItem assemblyVersion;

        private static MethodInfo parseTextMethod;

        public ReadAssemblyInfo() {
            if (parseTextMethod == null) {
                InitializeParseTextMethod();
            }
        }

        [Required]
        public string[] AssemblyInfoFiles { get; set; }

        [Output]
        public TaskItem AssemblyVersion {
            get { return assemblyVersion; }
        }

        [Output]
        public TaskItem AssemblyInformationalVersion {
            get { return assemblyInformationalVersion; }
        }

        [Output]
        public TaskItem AssemblyFileVersion {
            get { return assemblyFileVersion; }
        }

        public override bool Execute() {
            foreach (var file in AssemblyInfoFiles) {
                if (!File.Exists(file)) {
                    continue;
                }

                using (var reader = new StreamReader(file)) {
                    var text = reader.ReadToEnd();
                    ParseCSharpCode(text);
                }
            }

            return !Log.HasLoggedErrors;
        }

        internal void ParseCSharpCode(string text) {
            dynamic tree = parseTextMethod.Invoke(null, new object[] { text, null, "", null, CancellationToken.None });
            var root = tree.GetRoot();

            var attributeLists = root.DescendantNodes(); /*.OfType<AttributeListSyntax>();*/
            foreach (var p in attributeLists) {
                if (p.GetType().Name != "AttributeListSyntax") {
                    continue;
                }

                foreach (var attribute in p.Attributes) {
                    if (attribute.Name.GetType().Name != "IdentifierNameSyntax") {
                        continue;
                    }

                    var identifier = attribute.Name;

                    if (identifier != null) {
                        ParseAttribute("AssemblyInformationalVersion", identifier, attribute, ref assemblyInformationalVersion);
                        ParseAttribute("AssemblyVersion", identifier, attribute, ref assemblyVersion);
                        ParseAttribute("AssemblyFileVersion", identifier, attribute, ref assemblyFileVersion);
                    }
                }
            }
        }

        private void InitializeParseTextMethod() {
            string pathToBuildTools = ToolLocationHelper.GetPathToBuildTools(ToolLocationHelper.CurrentToolsVersion);
            var locator = new RoslynLocator(pathToBuildTools);

            parseTextMethod = locator.GetCodeAnalysisCSharpAssembly().GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree")
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(x => x.Name == "ParseText" && x.GetParameters()[0].ParameterType == typeof(string));
        }

        private static void SetMetadata(TaskItem taskItem, Version version) {
            taskItem.SetMetadata(nameof(version.Major), version.Major.ToString());
            taskItem.SetMetadata(nameof(version.Minor), version.Minor.ToString());
            taskItem.SetMetadata(nameof(version.Build), version.Build.ToString());
            taskItem.SetMetadata(nameof(version.Revision), version.Revision.ToString());
        }

        private static void ParseAttribute(string attributeName, dynamic identifier, dynamic attribute, ref TaskItem field) {
            if (field != null) {
                return;
            }

            if (identifier.Identifier.Text.IndexOf(attributeName, StringComparison.Ordinal) >= 0) {
                var listArgument = attribute.ArgumentList.Arguments[0];

                var rawText = listArgument.Expression.GetText().ToString();
                if (!string.IsNullOrWhiteSpace(rawText)) {
                    rawText = rawText.Replace("\"", "");
                    Version version;
                    if (Version.TryParse(rawText, out version)) {
                        field = new TaskItem(version.ToString());
                        SetMetadata(field, version);
                    }
                }
            }
        }
    }

}
