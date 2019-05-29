using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Aderant.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class ReadAssemblyInfo : Task {
        private static ConcurrentDictionary<string, Tuple<TaskItem, TaskItem, TaskItem>> infoCache = new ConcurrentDictionary<string, Tuple<TaskItem, TaskItem, TaskItem>>(StringComparer.OrdinalIgnoreCase);

        private static MethodInfo parseTextMethod;

        private TaskItem assemblyFileVersion;
        private TaskItem assemblyInformationalVersion;
        private TaskItem assemblyVersion;

        public ReadAssemblyInfo() {
            if (parseTextMethod == null) {
                InitializeParseTextMethod();
            }
        }

        [Required]
        public ITaskItem[] AssemblyInfoFiles { get; set; }

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
            if (AssemblyInfoFiles == null || AssemblyInfoFiles.Length == 0) {
                Log.LogMessage(MessageImportance.Low, "No files provided");
                return true;
            }

            if (AssemblyInfoFiles.Length > 1) {
                Log.LogError("More than 1 file provided: " + String.Join(",", AssemblyInfoFiles.Select(s => s.ItemSpec)));
                return false;
            }

            string assemblyInfoFile = AssemblyInfoFiles[0].GetMetadata("FullPath");

            if (!File.Exists(assemblyInfoFile)) {
                return true;
            }

            Tuple<TaskItem, TaskItem, TaskItem> attributes;
            if (infoCache.TryGetValue(assemblyInfoFile, out attributes)) {
                Log.LogMessage(MessageImportance.Low, $"Reading attributes for {assemblyInfoFile} from cache.");
                assemblyVersion = attributes.Item1;
                assemblyInformationalVersion = attributes.Item2;
                assemblyFileVersion = attributes.Item3;
                return true;
            }

            using (var reader = new StreamReader(assemblyInfoFile)) {
                var text = reader.ReadToEnd();
                ParseCSharpCode(text);

                infoCache[assemblyInfoFile] = Tuple.Create(AssemblyVersion, AssemblyInformationalVersion, AssemblyFileVersion);
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

            var assembly = locator.GetCodeAnalysisCSharpAssembly();

            ErrorUtilities.IsNotNull(assembly, nameof(assembly));

            parseTextMethod = assembly.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree")
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