using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.Logging {
    [TestClass]
    public class LoggingInvalidTemplateTests : AderantCodeFixVerifier<LoggingInvalidTemplateRule> {

        #region Tests

        [TestMethod]
        public void LoggingInvalidTemplate_Invalid() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {2}"",
                new object(),
                new object());
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

            VerifyCSharpDiagnostic(code, GetDiagnostic(10, 17));
        }

        [TestMethod]
        public void LoggingInvalidTemplate_Invalid_Formatted() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {2:0.000}"",
                new object(),
                new object());
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

            VerifyCSharpDiagnostic(code, GetDiagnostic(10, 17));
        }

        [TestMethod]
        public void LoggingInvalidTemplate_Invalid_NonZeroStart() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{1}, {2}"",
                new object(),
                new object());
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

            VerifyCSharpDiagnostic(code, GetDiagnostic(10, 17));
        }

        [TestMethod]
        public void LoggingInvalidTemplate_Valid() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {1}"",
                new object(),
                new object());
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
        public void LoggingInvalidTemplate_Valid_VariableTemplate_Field() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        private const string template = ""Template: {0} {1}"";
        private const string foo = ""Foo"";
        private const int bar = 4;

        public void TestMethod(ILogWriter logWriter) {
                logWriter.Log(
                LogLevel.Debug,
                template,
                foo,
                bar);
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
        public void LoggingInvalidTemplate_Valid_VariableTemplate_Local() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            const string template = ""Template: {0} {1}"";
            const string foo = ""Foo"";
            const int bar = 4;

            logWriter.Log(
                LogLevel.Error,
                template,
                new object(),
                new object());
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
        public void LoggingInvalidTemplate_Valid_Formatted() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {1}"",
                new object(),
                new object());
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
