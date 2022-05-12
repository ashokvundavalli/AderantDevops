using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class QueryServiceQueryAllTests : AderantCodeFixVerifier<QueryServiceQueryAllRule> {

        protected override string PreCode => @"
using System;
using System.Linq;
using System.Collections.Generic;
using Aderant.Framework.Extensions;
using Aderant.Query;
using Aderant.Query.Annotations;
using Aderant.Query.ViewModels;

namespace Test {
";

        protected override string PostCode => @"
}

namespace Aderant.Framework.Extensions {
    public static class QueryableExtensions {
        public static IQueryable<T> Expand<T>(this IQueryable<T> query, string text) {
            return null;
        }

        public static IEnumerable<T> WhereContainsBatched<T>(this IQueryable<T> queryable) {
            return null;
        }
    }
}

namespace Aderant.Query {
    public interface IQueryServiceProxy {
        IQueryable<ModelItem> ModelItems { get; }

        IQueryable<ModelItemCacheable> CacheableModelItems { get; }
    }

    public class QueryServiceProxy : IQueryServiceProxy {
        public IQueryable<ModelItem> ModelItems { get; }

        public IQueryable<ModelItemCacheable> CacheableModelItems { get; }
    }
}

namespace Aderant.Query.Annotations {
    public class IsCacheableAttribute : Attribute {
        // Empty.
    }
}

namespace Aderant.Query.ViewModels {
    public class ModelItem {
        // Empty.
    }

    [IsCacheable]
    public class ModelItemCacheable {
        // Empty.
    }
}
";


        [TestMethod]
        public async Task QueryServiceQueryAll_QueryExpression() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = (from modelItem in proxy.ModelItems where modelItem != null select modelItem).ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        [Ignore("Unsure what the verification here is. No code special cases WhereContainsBatched")]
        public async Task QueryServiceQueryAll_CaseExtensionMethod() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.WhereContainsBatched().ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Field_Diagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(16, 24));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Field_NoDiagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems;
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Local_Diagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            IQueryServiceProxy proxy = null;

            var test = proxy.ModelItems.ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(16, 24));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Local_NoDiagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            IQueryServiceProxy proxy = null;

            var test = proxy.ModelItems;
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Parameter_Diagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            // Empty.
        }

        public static void Foo(IQueryServiceProxy proxy) {
            var test = proxy.ModelItems.ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(18, 24));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Parameter_NoDiagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            // Empty.
        }

        public static void Foo(IQueryServiceProxy proxy) {
            var test = proxy.ModelItems;
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_FactoryCreated_Diagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            var test = CreateProxy().ModelItems.ToList();
        }

        public static IQueryServiceProxy CreateProxy() {
            return null;
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(14, 24));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_FactoryCreated_NoDiagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            var test = CreateProxy().ModelItems;
        }

        public static IQueryServiceProxy CreateProxy() {
            return null;
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Constructed_Diagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            var test = new QueryServiceProxy().ModelItems.ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(14, 24));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Constructed_NoDiagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            var test = new QueryServiceProxy().ModelItems;
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_FirstOrDefault_NoDiagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            var test = new QueryServiceProxy().ModelItems.FirstOrDefault();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Filtered_Diagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.ToList().Where(x => x.ToString() != null);
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(16, 24));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Filtered_NoDiagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.Where(x => x.ToString() != null).ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Expanded_Diagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.Expand(""Test"").ToList().Where(x => x.ToString() != null);
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(16, 24));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_NoDiagnostic_Expand() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.Expand(""Test"").Where(x => x.ToString() != null).ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_NoDiagnostic_MultiExpand() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.Expand(""Test"").Expand(""Test"").Where(x => x.ToString() != null).ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Cacheable_NoDiagnostic_Enumerated() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.CacheableModelItems.ToList();
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_Cacheable_NoDiagnostic_NotEnumerated() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.CacheableModelItems;
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_ForEach_Diagnostic_Expand() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            foreach (var modelItem in proxy.ModelItems.Expand(""Test"")) {
                // Empty.
            }
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(16, 39));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_ForEach_Diagnostic_ExplicitEnumeration() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            foreach (var modelItem in proxy.ModelItems.ToList()) {
                // Empty.
            }
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(16, 39));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_ForEach_Diagnostic_NoExplicitEnumeration() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            foreach (var modelItem in proxy.ModelItems) {
                // Empty.
            }
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(16, 39));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_ForEach_NoDiagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            foreach (var modelItem in proxy.ModelItems.Where(x => x.ToString() != null)) {
                // Empty.
            }
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_ForEach_NoDiagnostic_Cacheable() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            foreach (var modelItem in proxy.CacheableModelItems) {
                // Empty.
            }
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_ForEach_NoDiagnostic_Expand() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            foreach (var modelItem in proxy.ModelItems.Expand(""Test"").Where(x => x.ToString() != null)) {
                // Empty.
            }
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_ForEach_NoDiagnostic_MultiExpand() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            foreach (var modelItem in proxy.ModelItems.Expand(""Test"").Expand(""Test"").Where(x => x.ToString() != null)) {
                // Empty.
            }
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_MethodParameter_Diagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            Foo(proxy.ModelItems.Expand(""Test"").Expand(""Test"").ToList());
        }

        public static void Foo(IEnumerable<ModelItem> query) {

        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(16, 17));
        }

        [TestMethod]
        public async Task QueryServiceQueryAll_MethodParameter_NoDiagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            Foo(proxy.ModelItems);
        }

        public static void Foo(IEnumerable<ModelItem> query) {
            
        }
    }
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }
    }
}
