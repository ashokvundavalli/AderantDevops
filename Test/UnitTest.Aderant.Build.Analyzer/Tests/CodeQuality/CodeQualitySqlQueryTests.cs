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
        public void CodeQualitySqlQuery_All() {
            const string code = @"
using Aderant.Query.Services.Infrastructure;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var dbContext = new System.Data.Entity.DbContext();

            dbContext.Database.SqlQuery<object>(null, null);
            dbContext.Database.SqlQuery(null, null, null);
            dbContext.Database.SuppressEntitySummary().SqlQuery<object>(null, null);
            dbContext.Database.SuppressEntitySummary().SqlQuery(null, null, null);
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
        public static System.Data.Entity.Database SuppressEntitySummary(
            this System.Data.Entity.Database db) {
            return null;
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: dbContext.Database.SqlQuery<object>(null, null);
                GetDiagnostic(9, 13),
                // Error: dbContext.Database.SqlQuery(null, null, null);
                GetDiagnostic(10, 13),
                // Error: dbContext.Database.SuppressEntitySummary().SqlQuery<object>(null, null);
                GetDiagnostic(11, 13),
                // Error: dbContext.Database.SuppressEntitySummary().SqlQuery(null, null, null);
                GetDiagnostic(12, 13));
        }

        #endregion Tests
    }
}
