using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Helpers {
    internal class TextContextLogger : ILogger {
        private readonly TestContext testContext;

        public TextContextLogger(TestContext testContext) {
            this.testContext = testContext;
        }

        public void Debug(string message, params object[] args) {
            testContext.WriteLine(message, args);
        }

        public void Info(string message, params object[] args) {
            testContext.WriteLine(message, args);
        }

        public void Warning(string message, params object[] args) {
            testContext.WriteLine(message, args);
        }

        public void Error(string message, params object[] args) {
            testContext.WriteLine(message, args);
        }
    }
}