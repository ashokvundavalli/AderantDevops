using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace TestHelper {
    public static class CSharpAnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new() {

        public static DiagnosticResult Diagnostic()
            => CSharpAnalyzerVerifier<TAnalyzer, MSTestVerifier>.Diagnostic();

        public static DiagnosticResult Diagnostic(string diagnosticId)
            => CSharpAnalyzerVerifier<TAnalyzer, MSTestVerifier>.Diagnostic(diagnosticId);

        public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
            => new DiagnosticResult(descriptor);

        public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected) {
            var test = new Test { TestCode = source };
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }

        // Code fix tests support both analyzer and code fix testing. This test class is derived from the code fix test
        // to avoid the need to maintain duplicate copies of the customization work.
        public class Test : MSTestDotNet471Test<TAnalyzer, EmptyCodeFixProvider> {

            public Test() {
            }
        }
    }
}