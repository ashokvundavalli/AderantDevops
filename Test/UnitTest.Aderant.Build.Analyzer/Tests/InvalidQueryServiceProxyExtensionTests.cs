using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class InvalidQueryServiceProxyExtensionTests : AderantCodeFixVerifier<InvalidQueryServiceProxyExtensionRule> {

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
                var queryServiceProxy = new QueryServiceProxy();
                var queryServiceProxyAsInterface = new QueryServiceProxy() as IQueryServiceProxy;
";

        protected override string PostCode => @"
            }
        }
    }

    public interface IQueryServiceProxy {
        IQueryable<ExpansionCode> ExpansionCodes { get; }
    }

    public class QueryServiceProxy : IQueryServiceProxy {
        public IQueryable<ExpansionCode> ExpansionCodes { get; }
            
        public QueryServiceProxy() {
            ExpansionCodes = new List<ExpansionCode>().AsQueryable();
        }
    }

    public class ExpansionCode {
        public int Id { get; }
    }
";

        [TestMethod]
        public async Task Count_WithArguments_ShouldFail() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Count(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("Count");
            await VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public async Task Count_WithoutArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Count();");

            await VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public async Task Count_AfterWherewithArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Where(e => e.Id == 0).Count();");

            await VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public async Task Count_WithArguments_ShouldFail_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Count(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("Count");
            await VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public async Task Count_WithoutArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Count();");

            await VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public async Task Count_AfterWherewithArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Where(e => e.Id == 0).Count();");

            await VerifyCSharpDiagnostic(test);
        }
    }
}
