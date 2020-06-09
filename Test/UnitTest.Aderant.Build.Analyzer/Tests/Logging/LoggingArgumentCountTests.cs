using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.Logging {
    [TestClass]
    public class LoggingArgumentCountTests : AderantCodeFixVerifier {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingArgumentCountTests" /> class.
        /// </summary>
        public LoggingArgumentCountTests()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingArgumentCountTests"/> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public LoggingArgumentCountTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

        #region Properties

        protected override RuleBase Rule => new LoggingArgumentCountRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void LoggingArgumentCount_ArgumentMatch() {
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
        public void LoggingArgumentCount_ArgumentMatch_Exception() {
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
                new object(),
                new AccessViolationException());
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
        public void LoggingArgumentCount_ArgumentMatch_Exception_Argument() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter, AccessViolationException exception) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {1}"",
                new object(),
                new object(),
                (exception));
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
        public void LoggingArgumentCount_ArgumentMatch_Exception_LocalVariable() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            var exception = new AccessViolationException();

            logWriter.Log(
                LogLevel.Error,
                ""{0}, {1}"",
                new object(),
                new object(),
                exception);
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
        public void LoggingArgumentCount_ArgumentMatch_Formatted() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {1:0.0}"",
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
        public void LoggingArgumentCount_Distinct_Match() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {0}"",
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
        public void LoggingArgumentCount_Distinct_Match_Exception() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {0}"",
                new object(),
                new AccessViolationException());
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
        public void LoggingArgumentCount_Distinct_Mismatch() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {0}"",
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

            VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(
                    8,   // Line
                    13,  // Row
                    1,   // Expected # arguments
                    2)); // Actual # arguments
        }

        [TestMethod]
        public void LoggingArgumentCount_Distinct_Mismatch_Exception() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {0}"",
                new object(),
                new object(),
                new AccessViolationException());
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
                GetDiagnostic(
                    8,   // Line
                    13,  // Row
                    1,   // Expected # arguments
                    2)); // Actual # arguments
        }

        [TestMethod]
        public void LoggingArgumentCount_Distinct_Mismatch_Formatted() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                ""{0}, {0:0}"",
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

            VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(
                    8,   // Line
                    13,  // Row
                    1,   // Expected # arguments
                    2)); // Actual # arguments
        }

        [TestMethod]
        public void LoggingArgumentCount_Message() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, ""Test"");
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
        public void LoggingArgumentCount_MessageException() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(LogLevel.Error, ""Test"", new AccessViolationException());
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
        public void LoggingArgumentCount_TextTranslation() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                TextTranslator.Current.Translate(""Test{2}""),
                new object(),
                new object());
        }
    }

    public class TextTranslator {
        public static TextTranslator Current { get; }

        public string Translate(string test) {
            return test;
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
        public void LoggingArgumentCount_Variable_Field() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        const string test = ""{0}"";

        public void TestMethod(ILogWriter logWriter) {
            logWriter.Log(
                LogLevel.Error,
                test,
                new object(),
                new AccessViolationException());
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
        public void LoggingArgumentCount_Variable_Local() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            const string test = ""{0}"";

            logWriter.Log(
                LogLevel.Error,
                test,
                new object(),
                new AccessViolationException());
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
        public void LoggingArgumentCount_Variable_Local_Exception() {
            const string code = @"
using System;
using Aderant.Framework.Logging;

namespace Test {
    public class TestClass {
        public void TestMethod(ILogWriter logWriter) {
            const string test = ""{0}"";

            logWriter.Log(
                LogLevel.Error,
                test,
                new object(),
                new AccessViolationException());
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
