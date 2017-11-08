using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aderant.Build.Tasks {
    public class ReadAssemblyInfo : Task {
        private TaskItem assemblyFileVersion;

        private TaskItem assemblyInformationalVersion;
        private TaskItem assemblyVersion;

        static ReadAssemblyInfo() {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                var name = args.Name.Split(',')[0];
                if (name == "System.Collections.Immutable") {
                    var immutable = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(asm => asm.FullName.Split(',')[0] == "System.Collections.Immutable");
                    if (immutable != null) {
                        return immutable;
                    }
                    return Assembly.LoadFile($"{AppDomain.CurrentDomain.BaseDirectory}\\System.Collections.Immutable.dll");
                }
                return null;
            };
        }

        [Required]
        public string[] AssemblyInfoFiles { get; set; }

        [Output]
        public TaskItem AssemblyVersion => assemblyVersion;

        [Output]
        public TaskItem AssemblyInformationalVersion => assemblyInformationalVersion;

        [Output]
        public TaskItem AssemblyFileVersion => assemblyFileVersion;

        public override bool Execute() {

            foreach (var file in AssemblyInfoFiles) {

                if (!File.Exists(file)) {
                    continue;
                }

                using (var reader = new StreamReader(file)) {
                    var text = reader.ReadToEnd();

                    var tree = CSharpSyntaxTree.ParseText(text);
                    var root = (CompilationUnitSyntax)tree.GetRoot();

                    var attributeLists = root.DescendantNodes().OfType<AttributeListSyntax>();
                    foreach (var p in attributeLists) {
                        foreach (var attribute in p.Attributes) {
                            var identifier = attribute.Name as IdentifierNameSyntax;

                            if (identifier != null) {
                                ParseAttribute("AssemblyInformationalVersion", identifier, attribute, ref assemblyInformationalVersion);
                                ParseAttribute("AssemblyVersion", identifier, attribute, ref assemblyVersion);
                                ParseAttribute("AssemblyFileVersion", identifier, attribute, ref assemblyFileVersion);
                            }
                        }
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }

        private static void SetMetadata(TaskItem taskItem, Version version) {
            taskItem.SetMetadata(nameof(version.Major), version.Major.ToString());
            taskItem.SetMetadata(nameof(version.Minor), version.Minor.ToString());
            taskItem.SetMetadata(nameof(version.Build), version.Build.ToString());
            taskItem.SetMetadata(nameof(version.Revision), version.Revision.ToString());
        }

        private static void ParseAttribute(string attributeName, IdentifierNameSyntax identifier, AttributeSyntax attribute, ref TaskItem field) {
            if (field != null) {
                return;
            }

            if (identifier.Identifier.Text.IndexOf(attributeName, StringComparison.Ordinal) >= 0) {
                AttributeArgumentSyntax listArgument = attribute.ArgumentList.Arguments[0];

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
