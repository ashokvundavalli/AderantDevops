using System.IO;
using Aderant.Build;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.PackageRetrieval {
    [TestClass]
    [DeploymentItem("PackageRetrieval\\paket.dependencies", "PackageRetrieval")]
    public class PaketPackageManagerTests : IntegrationTestBase {

        public TestContext TestContext { get; set; }

        public string WorkingDirectory {
            get { return Path.Combine(TestContext.DeploymentDirectory, "PackageRetrieval"); }
        }

        [TestMethod]
        public void Package_download() {
            using (var packageManager = CreatePackageManager()) {
                packageManager.Update(true);
            }

            Assert.IsTrue(Directory.Exists(Path.Combine(WorkingDirectory, "packages", "Aderant.Build.Analyzer")));
        }

        private PaketPackageManager CreatePackageManager() {
            Assert.IsTrue(Directory.Exists(WorkingDirectory));

            return new PaketPackageManager(
                WorkingDirectory,
                new PhysicalFileSystem(),
                new WellKnownPackageSources(),
                new TextContextLogger(TestContext),
                true);
        }

    }

    internal class TextContextLogger : ILogger {

        private readonly TestContext testContext;

        public TextContextLogger(TestContext testContext) {
            this.testContext = testContext;
        }

        public void Debug(string message, params object[] args) {
            LogChecked(message, args);
        }

        public void Info(string message, params object[] args) {
            LogChecked(message, args);
        }

        public void Warning(string message, params object[] args) {
            LogChecked(message, args);
        }

        public void Error(string message, params object[] args) {
            LogChecked(message, args);
        }

        private void LogChecked(string message, object[] args) {
            if (args == null) {
                testContext.WriteLine(message);
            } else {
                testContext.WriteLine(message, args);
            }
        }

    }
}