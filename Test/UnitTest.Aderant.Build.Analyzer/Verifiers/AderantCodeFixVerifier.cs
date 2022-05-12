using System;
using System.Threading.Tasks;
using Aderant.Build.Analyzer;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace UnitTest.Aderant.Build.Analyzer.Verifiers {
    public abstract class AderantCodeFixVerifier<TRule> : AderantCodeFixVerifier<TRule, EmptyCodeFixProvider> where TRule : RuleBase, new() {

    }

    public abstract class AderantCodeFixVerifier<TRule, TCodeFix>
        where TRule : RuleBase, new()
        where TCodeFix : CodeFixProvider, new() {

        const string DefaultPath = "/Test0.cs";

        protected virtual string PreCode => @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using System.Data;
    using System.Data.SqlClient;

    namespace ConsoleApplication1 {
        class PROGRAM {
            static void Main(string[] args) {
                ";

        protected virtual string PostCode => @"
            }
        }
    }";

        protected int MyCodeStartsAtLine => PreCode.Split('\n').Length;

        [TestMethod]
        public void EmptyCodeWithNoViolationsPasses() {
            VerifyCSharpDiagnostic(string.Empty);
        }

        /// <summary>
        /// Delegates to <see cref="CSharpAnalyzerVerifier{TAnalyzer}"/> as a bridge between the recommended MS analyzer test pattern and the
        /// AderantCodeFixVerifier test pattern.
        /// </summary>
        /// <returns></returns>
        public Task VerifyCSharpDiagnostic(string source, params DiagnosticResult[] diagnosticResult) {
            return TestHelper.CSharpAnalyzerVerifier<AderantAnalyzer<TRule>>.VerifyAnalyzerAsync(source, diagnosticResult);
        }

        /// <summary>
        /// Delegates to <see cref="CSharpAnalyzerVerifier{TAnalyzer}"/> as a bridge between the recommended MS analyzer test pattern and the
        /// AderantCodeFixVerifier test pattern.
        /// </summary>
        public Task VerifyCSharpFix(string source, string fixedSource, params DiagnosticResult[] diagnosticResult) {
            return TestHelper.CSharpCodeFixVerifier<AderantAnalyzer<TRule>, TCodeFix>.VerifyCodeFixAsync(source, diagnosticResult, fixedSource);
        }

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        private RuleBase Rule { get; } = new TRule();


        /// <summary>
        /// Inserts the code between the pre and post code.
        /// </summary>
        /// <param name="codeToInsert">The code to insert.</param>
        protected string InsertCode(string codeToInsert) {
            return string.Concat(PreCode, codeToInsert, PostCode);
        }

        /// <summary>
        /// Gets the diagnostic - all that changes is where the error happens.
        /// </summary>
        /// <param name="lineNumber">The line number.</param>
        /// <param name="column">The column.</param>
        /// <param name="messageParameters">The message parameters.</param>
        protected DiagnosticResult GetDiagnostic(int lineNumber, int column, params object[] messageParameters) {
            return GenerateDiagnostic(
                Rule.Id,
                Rule.MessageFormat,
                Rule.Severity,
                lineNumber,
                column,
                messageParameters);
        }

        /// <summary>
        /// Gets the default diagnostic starting at line number <see cref="MyCodeStartsAtLine"/> and column 1.
        /// </summary>
        /// <param name="messageParameters">The message parameters.</param>
        protected DiagnosticResult GetDefaultDiagnostic(params object[] messageParameters) {
            return GetDiagnostic(MyCodeStartsAtLine, 1, messageParameters);
        }

        /// <summary>
        /// Generates the diagnostic.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="messageFormat">The message format.</param>
        /// <param name="severity">The severity.</param>
        /// <param name="lineNumber">The line number.</param>
        /// <param name="column">The column.</param>
        /// <param name="messageParameters">The message parameters.</param>
        protected DiagnosticResult GenerateDiagnostic(string id, string messageFormat, DiagnosticSeverity severity, int? lineNumber = null, int? column = null, object[] messageParameters = null) {
            var result = new DiagnosticResult(id, severity);

            if (messageFormat != null) {
                var message = CreateMessage(messageFormat, messageParameters);
                result.WithMessage(message);
            }

            result.WithDefaultPath(DefaultPath)
                .WithSeverity(severity);

            if (lineNumber != null && column != null) {
                return result.WithLocation(lineNumber.Value, column.Value);
            }

            return result;
        }

        private static string CreateMessage(string messageFormat, object[] messageParameters) {
            string message;
            if (messageParameters == null || messageParameters.Length == 0) {
                message = messageFormat;
            } else {
                message = string.Format(messageFormat, messageParameters);
            }

            return message;
        }

        protected DiagnosticResult GetDiagnostic() {
            return GenerateDiagnostic(
                Rule.Id,
                Rule.MessageFormat,
                Rule.Severity,
                null,
                null,
                null);
        }
    }
}