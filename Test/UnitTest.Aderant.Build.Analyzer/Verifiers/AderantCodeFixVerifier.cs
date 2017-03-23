using Aderant.Build.Analyzer;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace UnitTest.Aderant.Build.Analyzer.Verifiers {
    public abstract class AderantCodeFixVerifier : CodeFixVerifier {
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

        protected int MyCodeStartsAtLine { get; private set; }

        /// <summary>
        /// Get the CSharp analyzer being tested - to be implemented in non-abstract class
        /// </summary>
        /// <returns></returns>
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() {
            return new AderantAnalyzer(Rule);
        }

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        protected abstract RuleBase Rule { get; }

        [TestInitialize]
        public void InitializeTests() {
            MyCodeStartsAtLine = PreCode.Split('\n').Length;
        }

        //No diagnostics expected to show up
        [TestMethod]
        public void EmptyCodeWithNoViolationsPasses() {
            var test = @"";
            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Inserts the code between the pre and post code.
        /// </summary>
        /// <param name="codeToInsert">The code to insert.</param>
        /// <returns></returns>
        protected string InsertCode(string codeToInsert) {
            return string.Concat(PreCode, codeToInsert, PostCode);
        }

        /// <summary>
        /// Gets the diagnostic - all that changes is where the error happens.
        /// </summary>
        /// <param name="lineNumber">The line number.</param>
        /// <param name="column">The column.</param>
        /// <param name="messageParameters">The message parameters.</param>
        /// <returns></returns>
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
        /// <returns></returns>
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
        /// <returns></returns>
        protected DiagnosticResult GenerateDiagnostic(string id, string messageFormat, DiagnosticSeverity severity, int lineNumber, int column, object[] messageParameters) {
            return new DiagnosticResult {
                Id = id,
                Message = string.Format(messageFormat, messageParameters),
                Severity = severity,
                Locations = new[] { new DiagnosticResultLocation("Test0.cs", lineNumber, column) }
            };
        }
    }
}
