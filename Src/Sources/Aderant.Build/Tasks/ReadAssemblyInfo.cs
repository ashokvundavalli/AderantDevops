using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using Aderant.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class ReadAssemblyInfo : Task {

        private static readonly ConcurrentDictionary<(string, int), ITaskItem> infoCache = new ConcurrentDictionary<(string, int), ITaskItem>();

        private static Func<string, object> parseTextMethod;

        private object assemblyProductTitle;
        private object assemblyProductAttribute;
        private object assemblyFileVersionAttribute;
        private object assemblyVersionAttribute;
        private object assemblyInformationalVersionAttribute;
        private string assemblyInfoFile;
        private bool fileParsed;

        /// <summary>
        /// Controls if the inputs and outputs should be cached. Set this to false if it is unlikely the input will be seen again.
        /// </summary>
        public bool UseResultsCache { get; set; } = true;

        public ReadAssemblyInfo() {
            if (parseTextMethod == null) {
                InitializeParseTextMethod();
            }
        }

        [Required]
        public ITaskItem[] AssemblyInfoFiles { get; set; }


        [Output]
        public ITaskItem AssemblyVersion {
            get { return ParseAttributeArgumentList(ref assemblyVersionAttribute, 1); }
        }

        [Output]
        public ITaskItem AssemblyInformationalVersion {
            get { return ParseAttributeArgumentList(ref assemblyInformationalVersionAttribute, 2); }
        }

        [Output]
        public ITaskItem AssemblyFileVersion {
            get { return ParseAttributeArgumentList(ref assemblyFileVersionAttribute, 3); }
        }

        [Output]
        public ITaskItem AssemblyProduct {
            get { return ParseAttributeArgumentList(ref assemblyProductAttribute, 4, false); }
        }

        [Output]
        public ITaskItem AssemblyProductTitle {
            get { return ParseAttributeArgumentList(ref assemblyProductTitle, 5, false); }
        }

        [Output]
        public ITaskItem ProductName {
            get {
                var productTitle = AssemblyProductTitle;
                var assemblyProduct = AssemblyProduct;

                foreach (var version in new[] { productTitle, assemblyProduct }) {
                    if (version != null) {
                        return version;
                    }
                }

                return null;
            }
        }

        [Output]
        public ITaskItem ProductVersion {
            get {
                foreach (var version in new[] { AssemblyInformationalVersion, AssemblyFileVersion }) {
                    if (version != null && !string.IsNullOrEmpty(version.ItemSpec)) {
                        return version;
                    }
                }

                return null;
            }
        }

        public override bool Execute() {
            if (AssemblyInfoFiles == null || AssemblyInfoFiles.Length == 0) {
                Log.LogMessage(MessageImportance.Low, "No files provided", null);
                return true;
            }

            if (AssemblyInfoFiles.Length > 1) {
                Log.LogError("More than 1 file provided: " + string.Join(",", AssemblyInfoFiles.Select(s => s.ItemSpec)), null);
                return false;
            }

            assemblyInfoFile = AssemblyInfoFiles[0].GetMetadata("FullPath").ToUpperInvariant();

            var sentinel = (assemblyInfoFile, -1);

            if (infoCache.TryGetValue(sentinel, out _)) {
                // Sentinel found - parsing will happen when properties are accessed
                return true;
            }

            ReadFile(assemblyInfoFile);

            // Add sentinel marking the file as seen
            infoCache.TryAdd(sentinel, null);

            return !Log.HasLoggedErrors;
        }

        private void ReadFile(string file) {
            if (File.Exists(file)) {
                using (var reader = new StreamReader(file)) {
                    var text = reader.ReadToEnd();
                    ParseCSharpCode(text);
                }
            }

            fileParsed = true;
        }

        internal void ParseCSharpCode(string text) {
            dynamic tree = parseTextMethod.Invoke(text);
            var root = tree.GetRoot();

            var attributeLists = root.AttributeLists;
            foreach (var p in attributeLists) {
                if (p.GetType().Name != "AttributeListSyntax") {
                    continue;
                }

                foreach (var attribute in p.Attributes) {
                    if (attribute != null) {
                        if (attribute.Name.GetType().Name != "IdentifierNameSyntax") {
                            continue;
                        }

                        var identifier = attribute.Name;

                        if (identifier != null) {
                            HandleAttribute(identifier, attribute);
                        }
                    }
                }
            }
        }

        private void HandleAttribute(dynamic identifier, dynamic attribute) {
            var attributeName = (string)identifier.Identifier.Text;

            switch (attributeName) {
                case nameof(AssemblyInformationalVersionAttribute):
                case "AssemblyInformationalVersion": {
                    ConditionalAssign(ref assemblyInformationalVersionAttribute, attribute);
                    break;
                }
                case nameof(AssemblyVersionAttribute):
                case "AssemblyVersion": {
                    ConditionalAssign(ref assemblyVersionAttribute, attribute);
                    break;
                }
                case nameof(AssemblyFileVersionAttribute):
                case "AssemblyFileVersion": {
                    ConditionalAssign(ref assemblyFileVersionAttribute, attribute);
                    break;
                }
                case nameof(AssemblyProductAttribute):
                case "AssemblyProduct": {
                    ConditionalAssign(ref assemblyProductAttribute, attribute);
                    break;
                }
                case nameof(AssemblyTitleAttribute):
                case "AssemblyTitle": {
                    ConditionalAssign(ref assemblyProductTitle, attribute);
                    break;
                }
            }
        }

        private static void ConditionalAssign(ref object target, object attribute) {
            if (target == null) {
                target = attribute;
            }
        }

        private static void InitializeParseTextMethod() {
            string pathToBuildTools = ToolLocationHelper.GetPathToBuildTools(ToolLocationHelper.CurrentToolsVersion);
            var locator = new RoslynLocator(pathToBuildTools);

            var assembly = locator.GetCodeAnalysisCSharpAssembly();

            ErrorUtilities.IsNotNull(assembly, nameof(assembly));

            var methods = assembly.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree")
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.Name == "ParseText");

            foreach (var method in methods) {
                var parameters = method.GetParameters();

                if (parameters.Length == 5) {
                    if (parameters[0].ParameterType == typeof(string) &&
                        parameters[1].ParameterType.Name == "CSharpParseOptions" &&
                        parameters[2].ParameterType == typeof(string) &&
                        parameters[3].ParameterType == typeof(Encoding) &&
                        parameters[4].ParameterType == typeof(CancellationToken)) {
                        var textParam = Expression.Parameter(typeof(string), "text");

                        var call = Expression.Call(method,
                            textParam,
                            Expression.Constant(null, parameters[1].ParameterType),
                            Expression.Constant(null, parameters[2].ParameterType),
                            Expression.Constant(null, parameters[3].ParameterType),
                            Expression.Constant(CancellationToken.None, typeof(CancellationToken)));

                        var lambda = Expression.Lambda<Func<string, object>>(call, textParam);
                        var compile = lambda.Compile();

                        parseTextMethod = compile;
                        return;
                    }
                }
            }
        }

        private static void SetMetadata(ITaskItem taskItem, Version version) {
            taskItem.SetMetadata(nameof(version.Major), version.Major.ToString());
            taskItem.SetMetadata(nameof(version.Minor), version.Minor.ToString());
            taskItem.SetMetadata(nameof(version.Build), version.Build.ToString());
            taskItem.SetMetadata(nameof(version.Revision), version.Revision.ToString());
        }

        private ITaskItem ParseAttributeArgumentList(ref dynamic attribute, int i, bool parseVersion = true) {
            if (fileParsed && attribute == null) {
                return null;
            }

            if (infoCache.TryGetValue((assemblyInfoFile, i), out var result)) {
                return result;
            }

            if (!fileParsed) {
                ReadFile(assemblyInfoFile);
            }

            return ReadAttributeArgumentList(attribute, i, parseVersion);
        }

        private ITaskItem ReadAttributeArgumentList(dynamic attribute, int i, bool parseVersion) {
            var listArgument = attribute.ArgumentList.Arguments[0];

            var rawText = listArgument.Expression.GetText().ToString();

            if (!string.IsNullOrWhiteSpace(rawText)) {
                rawText = rawText.Replace("\"", "");

                ITaskItem result = null;
                if (parseVersion) {
                    if (Version.TryParse(rawText, out Version version)) {
                        result = new TaskItem(version.ToString());
                        SetMetadata(result, version);
                    }
                } else {
                    result = new TaskItem(rawText);
                }

                if (UseResultsCache) {
                    infoCache.TryAdd((assemblyInfoFile, i), result);
                }

                return result;
            }

            return null;
        }

    }
}