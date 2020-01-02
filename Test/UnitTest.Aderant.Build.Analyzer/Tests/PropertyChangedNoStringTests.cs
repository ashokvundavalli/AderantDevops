﻿using Aderant.Build.Analyzer.CodeFixes;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class PropertyChangedNoStringTests : AderantCodeFixVerifier {

        /// <summary>
        /// Returns the codefix being tested (C#) - to be implemented in non-abstract class
        /// </summary>
        /// <returns>
        /// The CodeFixProvider to be used for CSharp code
        /// </returns>
        protected override CodeFixProvider GetCSharpCodeFixProvider() {
            return new PropertyChangedNoStringFix();
        }

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        protected override RuleBase Rule => new PropertyChangedNoStringRule();

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

            static void OnPropertyChanged(string str) { 
                // do something
            }

            static void Main(string[] args) {
";

        protected override string PreCode => SharedPreCode;

        [TestMethod]
        public void PropertyChange_string_refers_to_class_member() {
            var test = InsertCode(@"OnPropertyChanged(""Test"");");

            var expected = GetDefaultDiagnostic("Test");
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"OnPropertyChanged(nameof(Test));");
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void PropertyChange_string_refers_to_baseclass_member() {
            var test = InsertCode(@"OnPropertyChanged(""Test2"");");

            var expected = GetDefaultDiagnostic("Test2");
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"OnPropertyChanged(nameof(Test2));");
            VerifyCSharpFix(test, fixtest);
        }
    }
}