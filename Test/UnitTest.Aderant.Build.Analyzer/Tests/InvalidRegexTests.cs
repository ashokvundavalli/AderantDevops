using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;
using VerifyCS = TestHelper.CSharpAnalyzerVerifier<Aderant.Build.Analyzer.AderantAnalyzer<Aderant.Build.Analyzer.Rules.InvalidRegexRule>>;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class InvalidRegexTests : AderantCodeFixVerifier<InvalidRegexRule> {

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
        public async Task RegexMatch_valid_regex() {
            var test = InsertCode(@"Regex.Match(""my text"", @""XXX"");");

            await VerifyCS.VerifyAnalyzerAsync(test);
        }


        [TestMethod]
        public async Task RegexMatch_invalid_regex() {
            var test = InsertCode(@"Regex.Match(""my text"", @""\pXXX"");");

            var expected = GetDefaultDiagnostic(@"parsing ""\pXXX"" - Malformed \p{X} character escape.");

            await new VerifyCS.Test {
                TestCode = test,
                TypesForAdditionalAssemblyReferences = new[] {
                    typeof(Regex)
                },
                ExpectedDiagnostics = { expected }
            }.RunAsync();
        }

    }
}