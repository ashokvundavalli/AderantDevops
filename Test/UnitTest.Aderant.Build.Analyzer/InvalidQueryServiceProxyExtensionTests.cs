using Aderant.Build.Analyzer;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer {
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
        public void FirstOrDefault_WithArguments_ShouldFail() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.FirstOrDefault(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("FirstOrDefault");
            VerifyCSharpDiagnostic(test, expected);
        }
        
        [TestMethod]
        public void FirstOrDefault_WithoutArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.FirstOrDefault();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void FirstOrDefault_AfterWherewithArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Where(e => e.Id == 0).FirstOrDefault();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void First_WithArguments_ShouldFail() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.First(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("First");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void First_WithoutArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.First();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void First_AfterWherewithArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Where(e => e.Id == 0).First();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SingleOrDefault_WithArguments_ShouldFail() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.SingleOrDefault(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("SingleOrDefault");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void SingleOrDefault_WithoutArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.SingleOrDefault();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SingleOrDefault_AfterWherewithArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Where(e => e.Id == 0).SingleOrDefault();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Single_WithArguments_ShouldFail() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Single(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("Single");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void Single_WithoutArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Single();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Single_AfterWherewithArguments_ShouldPass() {
            var test = InsertCode(@"queryServiceProxy.ExpansionCodes.Where(e => e.Id == 0).Single();");

            VerifyCSharpDiagnostic(test);
        }

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
        public void FirstOrDefault_WithArguments_ShouldFail_UsingInterface_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.FirstOrDefault(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("FirstOrDefault");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void FirstOrDefault_WithoutArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.FirstOrDefault();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void FirstOrDefault_AfterWherewithArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Where(e => e.Id == 0).FirstOrDefault();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void First_WithArguments_ShouldFail_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.First(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("First");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void First_WithoutArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.First();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void First_AfterWherewithArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Where(e => e.Id == 0).First();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SingleOrDefault_WithArguments_ShouldFail_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.SingleOrDefault(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("SingleOrDefault");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void SingleOrDefault_WithoutArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.SingleOrDefault();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SingleOrDefault_AfterWherewithArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Where(e => e.Id == 0).SingleOrDefault();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Single_WithArguments_ShouldFail_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Single(e => e.Id == 0);");

            var expected = GetDefaultDiagnostic("Single");
            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void Single_WithoutArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Single();");

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void Single_AfterWherewithArguments_ShouldPass_UsingInterface() {
            var test = InsertCode(@"queryServiceProxyAsInterface.ExpansionCodes.Where(e => e.Id == 0).Single();");

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