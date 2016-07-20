using System.Collections.Generic;
using Aderant.Build.Analyzer.CodeFixes;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer {
    [TestClass]
    public class AderantQueryServiceMigrationTests : AderantCodeFixVerifier {

        /// <summary>
        /// Returns the codefix being tested (C#) - to be implemented in non-abstract class
        /// </summary>
        /// <returns>
        /// The CodeFixProvider to be used for CSharp code
        /// </returns>
        protected override CodeFixProvider GetCSharpCodeFixProvider() {
            return new AderantQueryServiceMigrationCodeFix();
        }

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        protected override RuleBase Rule => new AderantQueryServiceMigrationRule();

        protected override string PreCode => @"
                using System;
                using System.Collections.Generic;
                using System.Linq;
                using System.Text;
                using System.Threading.Tasks;

                using ViewModels;
                namespace ViewModels { 
                    public class Client {}; 
                    public class Rule{
                        public int Id { get ; set; }
                        public bool IsActive { get ; set; }
                    }; 
                }
            
                public interface IQueryServiceProxy {
                }
                public interface IQueryServiceProxy<T> : IQueryServiceProxy where T : class {
                    T Methods { get; }
                }


                public interface IRatesQueryService {
                    void GetClientEmployeeRates(List<int> rankIds);
                }

                public class RateBatchQueryCriteria {
                    public IQueryServiceProxy<IRatesQueryService> QueryService { get; set; }
                    public List<int> RankIds { get; set; }
                    public string RateFilter { get; set; }
                }

                public interface IMyQS {}

                public interface IFactory {
                    T CreateInstance<T>();
                }
                public class Factory {
                    static IFactory fac;
                    public static IFactory Current { get { return fac; } }
                }

                namespace ConsoleApplication1 {
                public interface IQueryServiceProxy { IEnumerable<TRet> Query<TRet>(); }
                public interface IQueryServiceProxy<T> { IEnumerable<TRet> Query<TRet>(); }
                public class TypeName {
                    public T GetService<T>() {
                        return default(T);
                    }

";
        protected override string PostCode => @"           
                    }
                }
                ";

        public interface IQueryServiceProxy<T> { IEnumerable<TRet> Query<TRet>(); }

        [TestMethod]
        public void ParameterQsGetsFixedWithNamespace() {
            var test = InsertCode(@"
                        public void Foo(IQueryServiceProxy<IMyQS> qs) {
                            var bar = qs.Clients;
                        }");
            var expected = GetDiagnostic(MyCodeStartsAtLine + 2, 42);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public void Foo(IQueryServiceProxy<IMyQS> qs) {
                            var bar = qs.Query<ViewModels.Client>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }
        [TestMethod]
        public void OldQsCallGetsChangedToNewStyleAndClientGetsNamespace() {
            var test = InsertCode(@"
                        public void Foo() {
                            IQueryServiceProxy<IMyQS> QueryService;
                            var qux = QueryService.Clients;
                        }");

            var expected = GetDiagnostic(MyCodeStartsAtLine + 3, 52);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public void Foo() {
                            IQueryServiceProxy<IMyQS> QueryService;
                            var qux = QueryService.Query<ViewModels.Client>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }
        [TestMethod]
        public void MethodReturningQsProxyGetsFixed() {
            var test = InsertCode(@"
                        public void Foo() {
                            var qux = GetService<IQueryServiceProxy<IMyQS>>().Rules;
                        }");

            var expected = GetDiagnostic(MyCodeStartsAtLine + 2, 79);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public void Foo() {
                            var qux = GetService<IQueryServiceProxy<IMyQS>>().Query<Rule>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }
        [TestMethod]
        public void MethodReturningQsProxyGetsFixedAndTriviaLeftIntact() {
            var test = InsertCode(@"
                        public void Foo() {
                            var qux = GetService<IQueryServiceProxy<IMyQS>>()
                                .Rules
                                .Where(r => r.Id == 12);
                        }");

            var expected = GetDiagnostic(MyCodeStartsAtLine + 3, 34);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public void Foo() {
                            var qux = GetService<IQueryServiceProxy<IMyQS>>()
                                .Query<Rule>()
                                .Where(r => r.Id == 12);
                        }");
            VerifyCSharpFix(test, fixtest);
        }
        [TestMethod]
        public void MethodReturningQsProxyGetsFixedWithNamespace() {
            var test = InsertCode(@"
                        public void Foo() {
                            var qux = GetService<IQueryServiceProxy<IMyQS>>().Clients;
                        }");

            var expected = GetDiagnostic(MyCodeStartsAtLine + 2, 79);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public void Foo() {
                            var qux = GetService<IQueryServiceProxy<IMyQS>>().Query<ViewModels.Client>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void ParameterQsGetsFixed() {
            var test = InsertCode(@"
                        public void Foo(IQueryServiceProxy<IMyQS> qs) {
                            var bar = qs.Rules;
                        }");
            var expected = GetDiagnostic(MyCodeStartsAtLine + 2, 42);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public void Foo(IQueryServiceProxy<IMyQS> qs) {
                            var bar = qs.Query<Rule>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }
        [TestMethod]
        public void OldQsCallGetsChangedToNewStyleOnGenericInterface() {
            var test = InsertCode(@"
                        public void Foo() {
                            IQueryServiceProxy<IMyQS> QueryService;
                            var qux = QueryService.Clients;
                        }");

            var expected = GetDiagnostic(MyCodeStartsAtLine + 3, 52);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public void Foo() {
                            IQueryServiceProxy<IMyQS> QueryService;
                            var qux = QueryService.Query<ViewModels.Client>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void OldQsCallGetsChangedToNewStyleOnNonGenericInterface() {
            var test = InsertCode(@"
                        public void Foo() {
                            IQueryServiceProxy QueryService;
                            var qux = QueryService.Clients;
                        }");

            var expected = GetDiagnostic(MyCodeStartsAtLine + 3, 52);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public void Foo() {
                            IQueryServiceProxy QueryService;
                            var qux = QueryService.Query<ViewModels.Client>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }
        [TestMethod]
        public void OldQsCallGetsChangedToNewStyleOnReturn() {
            var test = InsertCode(@"
                        public object Foo() {
                            IQueryServiceProxy QueryService;
                            return QueryService.Clients;
                        }");

            var expected = GetDiagnostic(MyCodeStartsAtLine + 3, 49);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public object Foo() {
                            IQueryServiceProxy QueryService;
                            return QueryService.Query<ViewModels.Client>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void OldQsCallGetsChangedToNewStyleOnReturnWithLinqWhereClauseAdded() {
            var test = InsertCode(@"
                        public object Foo() {
                            IQueryServiceProxy QueryService;
                            return QueryService.Clients.Where(c => c.Id == 12);
                        }");

            var expected = GetDiagnostic(MyCodeStartsAtLine + 3, 49);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public object Foo() {
                            IQueryServiceProxy QueryService;
                            return QueryService.Query<ViewModels.Client>().Where(c => c.Id == 12);
                        }");
            VerifyCSharpFix(test, fixtest);
        }


        [TestMethod]
        public void PlainIQueryServiceProxyReturningValueGetsFixed() {
            var test = InsertCode(@"
                        public object Foo(IQueryServiceProxy queryServiceProxy) {
                            return queryServiceProxy
                                .Rules
                                .Where(a => a.Id == 12 && a.IsActive);
                        }");

            var expected = GetDiagnostic(MyCodeStartsAtLine + 3, 34);
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = InsertCode(@"
                        public object Foo(IQueryServiceProxy queryServiceProxy) {
                            return queryServiceProxy
                                .Query<Rule>()
                                .Where(a => a.Id == 12 && a.IsActive);
                        }");
            VerifyCSharpFix(test, fixtest);
        }

        //******** Stuff with no errors below, i.e. good code that does not need fixing is left alone  **************


        [TestMethod]
        public void MethodReturningQsProxyleftAloneIfNoErrors() {
            var test = InsertCode(@"
                        public void Foo() {
                            var qux = GetService<IQueryServiceProxy<IMyQS>>().Query<ViewModels.Client>();
                        }");

            VerifyCSharpDiagnostic(test);

            var fixtest = InsertCode(@"
                        public void Foo() {
                            var qux = GetService<IQueryServiceProxy<IMyQS>>().Query<ViewModels.Client>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }


        [TestMethod]
        public void QsProxyAssignedToVariableIsLeftAlone() {
            var test = InsertCode(@"
                        public void Foo() {
                            var qux = new { qsp = GetService<IQueryServiceProxy<IMyQS>>(),
                                otherThing = 12
                            };
                        }");

            VerifyCSharpDiagnostic(test);

            var fixtest = InsertCode(@"
                        public void Foo() {
                            var qux = new { qsp = GetService<IQueryServiceProxy<IMyQS>>(),
                                otherThing = 12
                            };
                        }");
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void VariableDeclarationsLeftAlone() {
            var test = InsertCode(@"
                        var qsp1 = Factory.Current.CreateInstance<IQueryServiceProxy>();
                        public void Foo() {
                            var qsp2 = Factory.Current.CreateInstance<IQueryServiceProxy>();
                        }");

            VerifyCSharpDiagnostic(test);

            var fixtest = InsertCode(@"
                        var qsp1 = Factory.Current.CreateInstance<IQueryServiceProxy>();
                        public void Foo() {
                            var qsp2 = Factory.Current.CreateInstance<IQueryServiceProxy>();
                        }");
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void NestedQueryServiceProxyCallLeftAlone() {
            var test = InsertCode(@"
                        public void Foo(RateBatchQueryCriteria criteria) {
                            IQueryable<Rule> ratesQuery = criteria.QueryService.Methods.GetClientEmployeeRates(criteria.RankIds);
                        }");

            VerifyCSharpDiagnostic(test);

            var fixtest = InsertCode(@"
                        public void Foo(RateBatchQueryCriteria criteria) {
                            IQueryable<Rule> ratesQuery = criteria.QueryService.Methods.GetClientEmployeeRates(criteria.RankIds);
                        }");
            VerifyCSharpFix(test, fixtest);
        }
    }
}