using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = TestHelper.CSharpCodeFixVerifier<Aderant.Build.Analyzer.AderantAnalyzer<Aderant.Build.Analyzer.Rules.PropertyChangedNoStringRule>, Aderant.Build.Analyzer.CodeFixes.PropertyChangedNoStringFix>;


namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class PropertyChangedNoStringTests {

        internal static string Code(string value) {

                return $@"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1 {{

        class Base {{ 
            protected static string Test2 {{ get; set; }}
        }}

        class Program : Base {{

            static string Test {{ get; set; }}

            static void OnPropertyChanged(string str) {{ 
                // do something
            }}

            static void Main(string[] args) {{
{value}
            }}
        }}
    }};
";
            }


        [TestMethod]
        public async Task PropertyChange_string_refers_to_class_member() {
            var test = Code(@"OnPropertyChanged(""Test"");");
            var fixtest = Code("OnPropertyChanged(nameof(Test));");

            await VerifyCS.VerifyCodeFixAsync(test, VerifyCS.Diagnostic().WithLocation(23, 1).WithArguments("Test"), fixtest);
        }

        [TestMethod]
        public async Task PropertyChange_string_refers_to_baseclass_member() {
            var test = Code(@"OnPropertyChanged(""Test2"");");
            var fixtest = Code("OnPropertyChanged(nameof(Test2));");

            await VerifyCS.VerifyCodeFixAsync(test, VerifyCS.Diagnostic().WithLocation(23, 1).WithArguments("Test2"), fixtest);
        }
    }
}

