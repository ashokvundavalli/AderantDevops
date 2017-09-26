﻿using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualityDbConnectionStringTests : AderantCodeFixVerifier {
        #region Properties

        protected override RuleBase Rule => new CodeQualityDbConnectionStringRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void CodeQualityDbConnectionString_All() {
            const string code = @"
using System.Data.Common;
using Aderant.Framework.Persistence;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            string connectionString = FrameworkDb
                .CreateConnection()
                .ConnectionString;
        }
    }
}

namespace Aderant.Framework.Persistence {
    public static class FrameworkDb {
        public static DbConnection CreateConnection() {
            return null;
        }
    }
}

namespace System.Data.Common {
    public abstract class DbConnection {
        public string ConnectionString { get; set; }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: string connectionString = FrameworkDb
                //            .CreateConnection()
                //            .ConnectionString;
                GetDiagnostic(8, 39));
        }

        #endregion Tests
    }
}