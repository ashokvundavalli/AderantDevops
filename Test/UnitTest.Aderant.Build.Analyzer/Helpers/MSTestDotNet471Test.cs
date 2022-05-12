using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace TestHelper {
    public abstract class MSTestDotNet471Test<TAnalyzer, TCodeFix> : CSharpCodeFixTest<TAnalyzer, TCodeFix, MSTestVerifier>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new() {

        private static readonly MetadataReference CorlibReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference SystemCoreReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        private static readonly MetadataReference CSharpSymbolsReference = MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location);
        private static readonly MetadataReference CodeAnalysisReference = MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location);
        private static readonly MetadataReference SqlClientReference = MetadataReference.CreateFromFile(typeof(SqlCommand).Assembly.Location);

        protected MSTestDotNet471Test() {
            ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net471.Default;

            TestState.AdditionalReferences.Add(CorlibReference);
            TestState.AdditionalReferences.Add(SystemCoreReference);
            TestState.AdditionalReferences.Add(CSharpSymbolsReference);
            TestState.AdditionalReferences.Add(CodeAnalysisReference);
            TestState.AdditionalReferences.Add(SqlClientReference);

            TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck;
        }

        public string EditorConfig {
            set {
                TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $@"root = true
[*]
{value}
"));
            }
        }

        public IEnumerable<Type> TypesForAdditionalAssemblyReferences {
            set {
                foreach (var typesForAdditionalAssemblyReference in value) {
                    TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typesForAdditionalAssemblyReference.Assembly.Location));
                }
            }
        }

    }
}