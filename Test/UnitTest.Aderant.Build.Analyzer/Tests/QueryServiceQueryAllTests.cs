using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class QueryServiceQueryAllTests : AderantCodeFixVerifier {
        protected override RuleBase Rule => new QueryServiceQueryAllRule();

        protected override string PreCode => @"
using System;
using System.Linq;
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
        public void QueryServiceQueryAll_Field_Diagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.ToList();
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(15, 24));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Field_NoDiagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems;
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Local_Diagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            IQueryServiceProxy proxy;

            var test = proxy.ModelItems.ToList();
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(15, 24));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Local_NoDiagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            IQueryServiceProxy proxy;

            var test = proxy.ModelItems;
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Parameter_Diagnostic() {
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(17, 24));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Parameter_NoDiagnostic() {
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_FactoryCreated_Diagnostic() {
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(13, 24));
        }

        [TestMethod]
        public void QueryServiceQueryAll_FactoryCreated_NoDiagnostic() {
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Constructed_Diagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            var test = new QueryServiceProxy().ModelItems.ToList();
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(13, 24));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Constructed_NoDiagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            var test = new QueryServiceProxy().ModelItems;
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_FirstOrDefault_NoDiagnostic() {
            const string test = @"
    public class Program {
        public static void Main() {
            var test = new QueryServiceProxy().ModelItems.FirstOrDefault();
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Filtered_Diagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.ToList().Where(x => x.ToString() != null);
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(15, 24));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Filtered_NoDiagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.Where(x => x.ToString() != null).ToList();
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Expanded_Diagnostic() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.ToList().Expand(""Test"").Where(x => x.ToString() != null);
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(15, 24));
        }

        [TestMethod]
        public void QueryServiceQueryAll_NoDiagnostic_Expand() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.Expand(""Test"").Where(x => x.ToString() != null).ToList();
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_NoDiagnostic_MultiExpand() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.ModelItems.Expand(""Test"").Expand(""Test"").Where(x => x.ToString() != null).ToList();
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Cacheable_NoDiagnostic_Enumerated() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.CacheableModelItems.ToList();
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_Cacheable_NoDiagnostic_NotEnumerated() {
            const string test = @"
    public class Program {
        private static readonly IQueryServiceProxy proxy;

        public static void Main() {
            var test = proxy.CacheableModelItems;
        }
    }
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_ForEach_Diagnostic_ExplicitEnumeration() {
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(15, 39));
        }

        [TestMethod]
        public void QueryServiceQueryAll_ForEach_Diagnostic_NoExplicitEnumeration() {
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(15, 39));
        }

        [TestMethod]
        public void QueryServiceQueryAll_ForEach_NoDiagnostic() {
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_ForEach_NoDiagnostic_Cacheable() {
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_ForEach_NoDiagnostic_Expand() {
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_ForEach_NoDiagnostic_MultiExpand() {
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void QueryServiceQueryAll_MethodParameter_Diagnostic() {
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(15, 17));
        }

        [TestMethod]
        public void QueryServiceQueryAll_MethodParameter_NoDiagnostic() {
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }
    }
}
