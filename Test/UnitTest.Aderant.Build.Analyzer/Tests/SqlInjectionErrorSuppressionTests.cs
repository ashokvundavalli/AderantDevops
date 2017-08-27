using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class SqlInjectionErrorSuppressionTests : SqlInjectionErrorTests {
        #region Tests: Suppression

        [TestMethod]
        public void SqlInjectionErrorSuppression_JunkAttribute() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            string test = Bar();

            Execute(test);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(""Junk"", ""Attribute"")]
        private static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(9, 21));
        }

        [TestMethod]
        public void SqlInjectionErrorSuppression_Aderant_Long() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            string test = Bar();

            Execute(test);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(""SQL Injection"", ""Aderant_SqlInjectionError"")]
        private static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionErrorSuppression_Aderant_Short() {
            const string test = @"
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class Program {
        public static void Main() {
            string test = Bar();

            Execute(test);
        }

        [SuppressMessage(""SQL Injection"", ""Aderant_SqlInjectionError"")]
        private static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionErrorSuppression_Microsoft_Long() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            string test = Bar();

            Execute(test);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(""Microsoft.Security"", ""CA2100:Review SQL queries for security vulnerabilities"")]
        private static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionErrorSuppression_Microsoft_Short() {
            const string test = @"
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class Program {
        public static void Main() {
            string test = Bar();

            Execute(test);
        }

        [SuppressMessage(""Microsoft.Security"", ""CA2100:Review SQL queries for security vulnerabilities"")]
        private static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionErrorSuppression_TestClass_Long() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
    public class Program {
        public static void Main() {
            string test = Bar();

            Execute(test);
        }

        private static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionErrorSuppression_TestClass_Short() {
            const string test = @"
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data.SqlClient;

namespace Test {
    [TestClass]
    public class Program {
        public static void Main() {
            string test = Bar();

            Execute(test);
        }

        private static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionErrorSuppression_Property_Long() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        private string someField = null;

        public string SomeProp {
            get { return string.Empty; }
            [System.Diagnostics.CodeAnalysis.SuppressMessage(""SQL Injection"", ""Aderant_SQLInjectionError"")]
            set {
                using (var connection = new SqlConnection()) {
                    using (var command = connection.CreateCommand()) {
                        command.CommandText = value;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionErrorSuppression_Property_Short() {
            const string test = @"
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class Program {
        private string someField = null;

        public string SomeProp {
            get { return string.Empty; }
            [SuppressMessage(""SQL Injection"", ""Aderant_SQLInjectionError"")]
            set {
                using (var connection = new SqlConnection()) {
                    using (var command = connection.CreateCommand()) {
                        command.CommandText = value;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        #endregion Tests: Suppression
    }
}
