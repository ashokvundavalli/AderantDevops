using System;
using System.Text.RegularExpressions;
using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class InvalidRegexTests : AderantCodeFixVerifier {

        protected override RuleBase Rule => new InvalidRegexRule();

        /// <summary>
        /// Gets the types for additional assembly references.
        /// </summary>
        protected override Type[] TypesForAddigionalAssemblyReferences => new Type[] { typeof(Regex) };

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
";

        [TestMethod]
        public void RegexMatch_valid_regex() {
            var test = InsertCode(@"Regex.Match(""my text"", @""XXX"");");

            VerifyCSharpDiagnostic(test);
        }


        [TestMethod]
        public void RegexMatch_invalid_regex() {
            var test = InsertCode(@"Regex.Match(""my text"", @""\pXXX"");");

            var expected = GetDefaultDiagnostic(@"parsing ""\pXXX"" - Malformed \p{X} character escape.");
            VerifyCSharpDiagnostic(test, expected);
        }
    }
}