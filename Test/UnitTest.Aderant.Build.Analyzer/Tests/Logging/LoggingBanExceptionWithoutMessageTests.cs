using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.Logging {
    [TestClass]
    public class LoggingBanExceptionWithoutMessageTests : AderantCodeFixVerifier {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingBanExceptionWithoutMessageTests"/> class.
        /// </summary>
        public LoggingBanExceptionWithoutMessageTests()
            : base(null) {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingBanExceptionWithoutMessageTests"/> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public LoggingBanExceptionWithoutMessageTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

        #region Properties

        protected override RuleBase Rule => new LoggingBanExceptionWithoutMessageRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void LoggingBanExceptionWithoutMessage_Valid() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, ""string describing the exception"", new AccessViolationException());
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
        public void LoggingBanExceptionWithoutMessage_Valid_NoException() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, ""string describing the exception"");
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
        public void LoggingBanExceptionWithoutMessage_Invalid() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, new AccessViolationException());
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

            VerifyCSharpDiagnostic(code, GetDiagnostic(8, 13));
        }

        [TestMethod]
        public void LoggingBanExceptionWithoutMessage_Exception_ExceptionProperty() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            var exception = new InvalidOperationException();

            logWriter.Log(LogLevel.Error, ((((exception)).Message)), exception);
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
                // Error: ((((exception)).Message))
                GetDiagnostic(10, 13));
        }

        [TestMethod]
        public void LoggingBanExceptionWithoutMessage_Exception_Property() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public string Foo { get; }

        public void TestMethod(ILogWriter logWriter) {
            var exception = new InvalidOperationException();

            logWriter.Log(LogLevel.Error, this.Foo, exception);
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
        public void LoggingBanExceptionWithoutMessage_ExceptionMessage() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            var exception = new InvalidOperationException();

            logWriter.Log(LogLevel.Error, exception.Message, exception);
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
                // Error: exception.Message
                GetDiagnostic(10, 13));
        }

        [TestMethod]
        public void LoggingBanExceptionWithoutMessage_ExceptionMessage_Valid() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, this.Message, exception);
        }

        public string Message { get; }
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
