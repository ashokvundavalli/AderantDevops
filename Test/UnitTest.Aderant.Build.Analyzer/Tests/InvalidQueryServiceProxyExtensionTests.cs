using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class InvalidQueryServiceProxyExtensionTests : AderantCodeFixVerifier {

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        protected override RuleBase Rule => new InvalidQueryServiceProxyExtensionRule();
        
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
        public void Count_WithArguments_ShouldFail() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Count(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("Count");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void Count_WithoutArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Count();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Count_AfterWherewithArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Where(e => e.Id == 0).Count();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Count_WithArguments_ShouldFail_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Count(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("Count");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void Count_WithoutArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Count();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Count_AfterWherewithArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Where(e => e.Id == 0).Count();");

            VerifyCSharpDiagnostic(test);
        }
    }
}