using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class InvalidLogMessageTests : AderantCodeFixVerifier {

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        protected override RuleBase Rule => new InvalidLogMessageRule();
        
        protected override string PreCode => @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1 {
        class PROGRAM {
            static void Main(string[] args) {
                Aderant.Framework.Logging.ILogWriter logWriter = new Aderant.Framework.Logging.LogWriter();
                int a = 0;
                double b = 1.5;
                string c = ""check"";
                Exception ex = new Exception();
                NullReferenceException nrex = new NullReferenceException();
";

        protected override string PostCode => @"
            }
        }
    }

    namespace Aderant.Framework.Logging {
        public interface ILogWriter {
            object Log(LogLevel level, string messageTemplate, params object[] detail);
        }

        public class LogWriter : ILogWriter {
            public object Log(LogLevel level, string messageTemplate, params object[] detail) {
                return null;
            }
        }

        public enum LogLevel {
            Debug
        }
    }
";

        [TestMethod]
        public void LogMessage_NoTemplateParts_NoParameters() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test"");");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_1TemplatePart_NoParameters() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}"");");

            var expected = GetDefaultDiagnostic(0, "Test {0}");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void LogMessage_1TemplatePartOtherThan0_NoParameters() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {1}"");");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_1TemplatePart_1Parameter_NonException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}"", a);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_1TemplatePartWithFormatting_1Parameter_NonException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0:O}"", a);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_1TemplatePart_1Parameter_Exception() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}"", ex);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_1TemplatePart_1Parameter_NullReferenceException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}"", nrex);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_1TemplatePart_2Parameters_LastIsException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}"", a, ex);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_1TemplatePart_2Parameters_LastIsNullReferenceException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}"", a, nrex);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_1Parameter_NonException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", a);");

            var expected = GetDefaultDiagnostic(1, "Test {0}, {1}");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_1Parameter_Exception() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", ex);");

            var expected = GetDefaultDiagnostic(1, "Test {0}, {1}");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_1Parameter_NullReferenceException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", nrex);");

            var expected = GetDefaultDiagnostic(1, "Test {0}, {1}");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_2Parameters_NonException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", a, b);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_2Parameters_Exception() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", a, ex);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_2Parameters_NullReferenceException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", a, nrex);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_3Parameters_LastIsNonException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", a, b, c);");

            var expected = GetDefaultDiagnostic(3, "Test {0}, {1}");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_3Parameters_SecondLastIsException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", a, ex, c);");

            var expected = GetDefaultDiagnostic(3, "Test {0}, {1}");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_3Parameters_SecondLastIsNullReferenceException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", a, nrex, c);");

            var expected = GetDefaultDiagnostic(3, "Test {0}, {1}");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_3Parameters_LastIsException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", a, b, ex);");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void LogMessage_2TemplateParts_3Parameters_LastIsNullReferenceException() {
            var test = InsertCode(@"logWriter.Log(Aderant.Framework.Logging.LogLevel.Debug, ""Test {0}, {1}"", a, b, nrex);");

            VerifyCSharpDiagnostic(test);
        }
    }
}