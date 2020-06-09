using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.Logging {
    [TestClass]
    public class LoggingInterpolationTests : AderantCodeFixVerifier {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingInterpolationTests"/> class.
        /// </summary>
        public LoggingInterpolationTests()
            : base(null) {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingInterpolationTests"/> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public LoggingInterpolationTests(RuleBase[] injectedRules)
            : base(injectedRules) {

        }

        #endregion Constructors

        #region Properties

        protected override RuleBase Rule => new LoggingInterpolationRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void LoggingInterpolation_Concatenation() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, ""Foo"" + ""Bar"");
        }
    }
}

namespace Aderant.Framework.Logging {
    public enum LogLevel {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public interface ILogWriter {
        bool IsEnabled(LogLevel level);
        object Log(LogLevel level, string message);
        object Log(LogLevel level, string messageTemplate, params object[] detail);
        object Log(LogLevel level, Exception exception);
        object Log(LogLevel level, string summaryMessage, Exception exception);
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void LoggingInterpolation_Concatenation_Field() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        const string foo = ""Foo"";

        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, foo + ""Bar"");
        }
    }
}

namespace Aderant.Framework.Logging {
    public enum LogLevel {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public interface ILogWriter {
        bool IsEnabled(LogLevel level);
        object Log(LogLevel level, string message);
        object Log(LogLevel level, string messageTemplate, params object[] detail);
        object Log(LogLevel level, Exception exception);
        object Log(LogLevel level, string summaryMessage, Exception exception);
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void LoggingInterpolation_Concatenation_Local() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            const string foo = ""Foo"";

            logWriter.Log(LogLevel.Error, foo + ""Bar"");
        }
    }
}

namespace Aderant.Framework.Logging {
    public enum LogLevel {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public interface ILogWriter {
        bool IsEnabled(LogLevel level);
        object Log(LogLevel level, string message);
        object Log(LogLevel level, string messageTemplate, params object[] detail);
        object Log(LogLevel level, Exception exception);
        object Log(LogLevel level, string summaryMessage, Exception exception);
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void LoggingInterpolation_Interpolation() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, $""{""Foo""}{""Bar""}"");
        }
    }
}

namespace Aderant.Framework.Logging {
    public enum LogLevel {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public interface ILogWriter {
        bool IsEnabled(LogLevel level);
        object Log(LogLevel level, string message);
        object Log(LogLevel level, string messageTemplate, params object[] detail);
        object Log(LogLevel level, Exception exception);
        object Log(LogLevel level, string summaryMessage, Exception exception);
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: $"{"Foo"}{"Bar"}"
                GetDiagnostic(8, 43));
        }

        [TestMethod]
        public void LoggingInterpolation_StringFormat_IntrinsicType() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, ((string.Format(""{0}{1}"", ""Foo"", ""Bar""))));
        }
    }
}

namespace Aderant.Framework.Logging {
    public enum LogLevel {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public interface ILogWriter {
        bool IsEnabled(LogLevel level);
        object Log(LogLevel level, string message);
        object Log(LogLevel level, string messageTemplate, params object[] detail);
        object Log(LogLevel level, Exception exception);
        object Log(LogLevel level, string summaryMessage, Exception exception);
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: ((string.Format("{0}{1}", "Foo", "Bar")))
                GetDiagnostic(8, 45));
        }

        [TestMethod]
        public void LoggingInterpolation_StringFormat_StrongType() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, ((String.Format(""{0}{1}"", ""Foo"", ""Bar""))));
        }
    }
}

namespace Aderant.Framework.Logging {
    public enum LogLevel {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public interface ILogWriter {
        bool IsEnabled(LogLevel level);
        object Log(LogLevel level, string message);
        object Log(LogLevel level, string messageTemplate, params object[] detail);
        object Log(LogLevel level, Exception exception);
        object Log(LogLevel level, string summaryMessage, Exception exception);
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: ((string.Format("{0}{1}", "Foo", "Bar")))
                GetDiagnostic(8, 45));
        }

        [TestMethod]
        public void LoggingInterpolation_Valid() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, ((""{0}{1}"")), new object(), new object());
        }
    }
}

namespace Aderant.Framework.Logging {
    public enum LogLevel {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public interface ILogWriter {
        bool IsEnabled(LogLevel level);
        object Log(LogLevel level, string message);
        object Log(LogLevel level, string messageTemplate, params object[] detail);
        object Log(LogLevel level, Exception exception);
        object Log(LogLevel level, string summaryMessage, Exception exception);
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
