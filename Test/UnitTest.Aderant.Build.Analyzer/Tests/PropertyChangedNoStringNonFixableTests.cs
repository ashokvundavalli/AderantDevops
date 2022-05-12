using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = TestHelper.CSharpAnalyzerVerifier<Aderant.Build.Analyzer.AderantAnalyzer<Aderant.Build.Analyzer.Rules.PropertyChangedNoStringNonFixableRule>>;


namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class PropertyChangedNoStringNonFixableTests {

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
        public async Task PropertyChange_string_refers_to_non_member() {
            var test = Code(@"OnPropertyChanged(""Test3"");");

            var diagnosticResult = VerifyCS.Diagnostic().WithLocation(23, 1).WithArguments("Test3");
            await VerifyCS.VerifyAnalyzerAsync(test, diagnosticResult);
        }
    }
}