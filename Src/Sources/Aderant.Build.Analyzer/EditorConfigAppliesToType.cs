using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer {
    internal class EditorConfigAppliesToType {

        public EditorConfigAppliesToType(string type) {
            Type = type;
        }

        public bool AppliesToDerived { get; set; }

        public string Type { get; }

        public static IEnumerable<EditorConfigAppliesToType> CreateAppliesTo(IEnumerable<string> appliesTo) {
            foreach (var type in appliesTo) {
                var parts = type.Split(':');
                if (parts.Length == 2) {
                    yield return new EditorConfigAppliesToType(parts[1]) {
                        AppliesToDerived = string.Equals(parts[0], "derived", StringComparison.OrdinalIgnoreCase)
                    };
                } else {
                    yield return new EditorConfigAppliesToType(parts[0]) {
                        AppliesToDerived = false
                    };
                }
            }
        }

        public static List<EditorConfigAppliesToType> GetEditorConfigAppliesToType(AnalyzerOptions analyzerOptions, SyntaxTree syntaxTree, string diagnosticId) {
            var appliesTo = analyzerOptions.GetConfigurationValues(syntaxTree, "dotnet_code_quality.aderant." + diagnosticId + ".applies_to", Array.Empty<string>());
            return CreateAppliesTo(appliesTo).ToList();
        }

        public static bool AppliesToContainsSymbol(List<EditorConfigAppliesToType> appliesTo, INamedTypeSymbol symbol) {
            if (symbol != null) {
                var baseTypesAndThis = symbol.GetBaseTypesAndThis();

                foreach (var type in baseTypesAndThis) {
                    for (var i = 0; i < appliesTo.Count; i++) {
                        var option = appliesTo[i];
                        string symbolName;

                        if (option.Type.Contains(".")) {
                            symbolName = type.ToString();
                        } else {
                            symbolName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                        }

                        if (option.Type == symbolName) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}