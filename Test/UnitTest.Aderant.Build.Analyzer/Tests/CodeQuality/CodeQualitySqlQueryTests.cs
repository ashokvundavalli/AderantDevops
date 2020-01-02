using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualitySqlQueryTests : AderantCodeFixVerifier {
        #region Properties

        protected override RuleBase Rule => new CodeQualitySqlQueryRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void CodeQualitySqlQuery_Invalid_DerivedExpertDbContext() {
            const string code = @"
using System.Data.Entity;
using Aderant.Query.Services;
using Aderant.Query.Services.Infrastructure;

namespace Test {
    public class TestClass : ExpertDbContext, IDisposable {
        public void TestMethod() {
            Database.SqlQuery<object>(null, null);
            Database.SqlQuery(null, null, null);
            Database.SuppressEntitySummary().SqlQuery<object>(null, null);
            Database.SuppressEntitySummary().SqlQuery(null, null, null);
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string parm0, params object[] parm1) {
            SqlQuery(typeof(TElement), parm0, parm1);
        }

        public void SqlQuery(Type parm0, string parm1, params object[] parm2) {
            // Empty
        }
    }

    public class DbContext {
        public Database Database => null;
    }
}

namespace Aderant.Query.Services {
    public class ExpertDbContext : DbContext {
        public void TestMethodBase() {
            // Empty.
        }
    }
}

namespace Aderant.Query.Services.Infrastructure {
    public static class DatabaseExtensions {
        public static Database SuppressEntitySummary(
            this Database db) {
            return null;
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: Database.SqlQuery<object>(null, null);
                GetDiagnostic(9, 13),
                // Error: Database.SqlQuery(null, null, null);
                GetDiagnostic(10, 13),
                // Error: Database.SuppressEntitySummary().SqlQuery<object>(null, null);
                GetDiagnostic(11, 13),
                // Error: Database.SuppressEntitySummary().SqlQuery(null, null, null);
                GetDiagnostic(12, 13));
        }

        [TestMethod]
        public void CodeQualitySqlQuery_Invalid_ExpertDbContext() {
            const string code = @"
using System.Data.Entity;
using Aderant.Query.Services;
using Aderant.Query.Services.Infrastructure;

namespace Test {
    public class TestClass : ExpertDbContext, IDisposable {
        public void TestMethod() {
            // Empty.
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string parm0, params object[] parm1) {
            SqlQuery(typeof(TElement), parm0, parm1);
        }

        public void SqlQuery(Type parm0, string parm1, params object[] parm2) {
            // Empty
        }
    }

    public class DbContext {
        public Database Database => null;
    }
}

namespace Aderant.Query.Services {
    public class ExpertDbContext : DbContext {
        public void TestMethodBase() {
            Database.SqlQuery<object>(null, null);
            Database.SqlQuery(null, null, null);
            Database.SuppressEntitySummary().SqlQuery<object>(null, null);
            Database.SuppressEntitySummary().SqlQuery(null, null, null);
        }
    }
}

namespace Aderant.Query.Services.Infrastructure {
    public static class DatabaseExtensions {
        public static Database SuppressEntitySummary(
            this Database db) {
            return null;
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: Database.SqlQuery<object>(null, null);
                GetDiagnostic(37, 13),
                // Error: Database.SqlQuery(null, null, null);
                GetDiagnostic(38, 13),
                // Error: Database.SuppressEntitySummary().SqlQuery<object>(null, null);
                GetDiagnostic(39, 13),
                // Error: Database.SuppressEntitySummary().SqlQuery(null, null, null);
                GetDiagnostic(40, 13));
        }

        [TestMethod]
        public void CodeQualitySqlQuery_Valid() {
            const string code = @"
using System.Data.Entity;
using Aderant.Query.Services.Infrastructure;

namespace Test {
    public class TestClass : DbContext {
        public void TestMethod() {
            Database.SqlQuery<object>(null, null);
            Database.SqlQuery(null, null, null);
            Database.SuppressEntitySummary().SqlQuery<object>(null, null);
            Database.SuppressEntitySummary().SqlQuery(null, null, null);
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string parm0, params object[] parm1) {
            SqlQuery(typeof(TElement), parm0, parm1);
        }

        public void SqlQuery(Type parm0, string parm1, params object[] parm2) {
            // Empty
        }
    }

    public class DbContext {
        public Database Database => null;
    }
}
namespace Aderant.Query.Services.Infrastructure {
    public static class DatabaseExtensions {
        public static Database SuppressEntitySummary(
            this Database db) {
            return null;
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
