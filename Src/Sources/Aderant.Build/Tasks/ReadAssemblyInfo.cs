using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class ReadAssemblyInfo : Task {
        private TaskItem assemblyFileVersion;
        private TaskItem assemblyInformationalVersion;
        private TaskItem assemblyVersion;

        private static readonly MethodInfo ParseTextMethod;
        private static readonly List<string> AcceptedCodeAnalysisCSharpVersions = new List<string> {
            "1.3.1.0",
            "1.2.0.0",
            "1.0.0.0"
        };


        static ReadAssemblyInfo() {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                var name = args.Name.Split(',')[0];
                if (name == "System.Collections.Immutable") {
                    var immutable = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(asm => asm.FullName.Split(',')[0] == "System.Collections.Immutable");
                    if (immutable != null) {
                        return immutable;
                    }
                    return
                        Assembly.LoadFile($"{AppDomain.CurrentDomain.BaseDirectory}\\System.Collections.Immutable.dll");
                }
                return null;
            };

            ParseTextMethod = GetCodeAnalysisCSharpAssembly().GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree")
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(x => x.Name == "ParseText" && x.GetParameters()[0].ParameterType == typeof(string));
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


                    dynamic tree = ParseTextMethod.Invoke(null, new object[] {text, null, "", null, CancellationToken.None});
                    var root = tree.GetRoot();

                    var attributeLists = root.DescendantNodes();/*.OfType<AttributeListSyntax>();*/
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
                                ParseAttribute("AssemblyInformationalVersion", identifier, attribute,
                                    ref assemblyInformationalVersion);
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

        private static Assembly GetCodeAnalysisCSharpAssembly() {
            const string fullNameUnformatted =
                "Microsoft.CodeAnalysis.CSharp, Version={0}, Culture=neutral, PublicKeyToken=31bf3856ad364e35";

            //try load each of the accepted versions
            foreach (var version in AcceptedCodeAnalysisCSharpVersions) {
                try {
                    return Assembly.Load(string.Format(fullNameUnformatted, version));
                } catch {
                    continue;
                }
            }

            //If we can not find, load by name only
            try {
                return Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.CSharp"));
            } catch {
                throw new Exception("Could not find Roslyn binary (Microsoft.CodeAnalysis.CSharp)");
            }
        }
    }
}