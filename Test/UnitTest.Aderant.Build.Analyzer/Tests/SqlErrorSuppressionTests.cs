using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class SqlErrorSuppressionTests : SqlInjectionErrorTests {
        [TestMethod]
        public void Analyzer_respects_FxCop_suppression() {
            const string test = @"

using System;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class Program {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(""Microsoft.Security"", ""CA2100:Review SQL queries for security vulnerabilities"", Justification = ""The SQL query is parameterized"")]
        public static void Main() {
            string test = ""Test"";

            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = test;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.Common.DbCommand CreateCommand() {
            return new System.Data.Common.DbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Common {
    public class DbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void SqlInjectionError_Attribute_SuppressMessage() {
            const string test = @"
using System;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class Program {
        [SuppressMessage(""SQL Injection"", ""Aderant_SqlInjection"")]
        public static void Main() {
            string test = ""Test"";

            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = test;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.Common.DbCommand CreateCommand() {
            return new System.Data.Common.DbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Common {
    public class DbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void SqlInjectionError_Attribute_SuppressMessage_MultipleAttributes_SideBySide() {
            const string test = @"
using System;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class Program {
        [SuppressMessage(""SQL Injection"", ""Aderant_SqlInjection""), Obsolete]
        public static void Main() {
            string test = ""Test"";

            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = test;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.Common.DbCommand CreateCommand() {
            return new System.Data.Common.DbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Common {
    public class DbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void SqlInjectionError_Attribute_SuppressMessage_MultipleAttributes_Stacked() {
            const string test = @"
using System;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class Program {
        [Obsolete]
        [SuppressMessage(""SQL Injection"", ""Aderant_SqlInjection"")]
        public static void Main() {
            string test = ""Test"";

            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = test;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.Common.DbCommand CreateCommand() {
            return new System.Data.Common.DbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Common {
    public class DbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }
    }
}