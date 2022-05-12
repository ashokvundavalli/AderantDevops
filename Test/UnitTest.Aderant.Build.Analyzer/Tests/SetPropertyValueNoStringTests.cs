using System.Threading.Tasks;
using Aderant.Build.Analyzer.CodeFixes;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;
using VerifyCS = TestHelper.CSharpCodeFixVerifier<Aderant.Build.Analyzer.AderantAnalyzer<Aderant.Build.Analyzer.Rules.SetPropertyValueNoStringRule>,
    Aderant.Build.Analyzer.CodeFixes.SetPropertyValueNoStringFix>;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class SetPropertyValueNoStringTests : AderantCodeFixVerifier<SetPropertyValueNoStringRule, SetPropertyValueNoStringFix> {

        internal static string SharedPreCode => @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1 {

        class Base { 
            protected static string Test2 { get; set; }
        }

        class Program : Base {

            static string Test { get; set; }

            static void SetPropertyValue(string str, int a, int b) { 
                // do something
            }

            static void Main(string[] args) {
";

        protected override string PreCode => SharedPreCode;

        [TestMethod]
        public async Task SetPropertyValue_string_refers_to_class_member() {
            var test = InsertCode(@"SetPropertyValue(""Test"", 0, 0);");

            var expected = GetDefaultDiagnostic("Test");

            var fixtest = InsertCode(@"SetPropertyValue(nameof(Test), 0, 0);");

            await new VerifyCS.Test {
                TestCode = test,
                FixedCode = fixtest,
                ExpectedDiagnostics = { expected },
            }.RunAsync();
        }

        [TestMethod]
        public async Task SetPropertyValue_string_refers_to_baseclass_member() {
            var test = InsertCode(@"SetPropertyValue(""Test2"", 0, 0);");

            var expected = GetDefaultDiagnostic("Test2");

            var fixtest = InsertCode(@"SetPropertyValue(nameof(Test2), 0, 0);");

            await new VerifyCS.Test {
                TestCode = test,
                FixedCode = fixtest,
                ExpectedDiagnostics = { expected },
            }.RunAsync();
        }

    }
}