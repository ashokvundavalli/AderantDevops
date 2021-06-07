using System;
using System.IO;
using Aderant.Build;
using Aderant.Build.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Commands {
    [TestClass]
    public class GetDependenciesTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Resources\sln.DotSettings")]
        public void ReSharperFileWriteTest() {
            var getDependencies = new GetDependencies(new PhysicalFileSystem());

            string destinationDirectory = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());

            Directory.CreateDirectory(destinationDirectory);

            string solutionFile = Path.Combine(destinationDirectory, string.Concat(Path.GetRandomFileName(), ".sln"));

            using (File.Create(solutionFile)) {
            }

            getDependencies.CopyReSharperSettings(File.ReadAllText(Path.Combine(TestContext.DeploymentDirectory, GetDependencies.ReSharperSettings)), destinationDirectory, TestContext.DeploymentDirectory);

            string destinationFile = Path.Combine(destinationDirectory, GetDependencies.GetReSharperSettingsFileName(solutionFile));

            Assert.IsTrue(File.Exists(destinationFile));

            string content = File.ReadAllText(destinationFile);

            Assert.AreEqual(-1, content.IndexOf(GetDependencies.AbsolutePathToken, StringComparison.Ordinal), $"{nameof(GetDependencies.AbsolutePathToken)} was not replaced.");
            Assert.AreEqual(-1, content.IndexOf(GetDependencies.RelativePathToken, StringComparison.Ordinal), $"{nameof(GetDependencies.RelativePathToken)} was not replaced.");
        }

        [TestMethod]
        [DeploymentItem(@"Resources\.editorconfig")]
        public void HardLinkTest() {
            var getDependencies = new GetDependencies(new PhysicalFileSystem());

            string destinationDirectory = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());

            Directory.CreateDirectory(destinationDirectory);

            getDependencies.LinkFile(TestContext.DeploymentDirectory, GetDependencies.EditorConfig, destinationDirectory);

            Assert.IsTrue(File.Exists(Path.Combine(destinationDirectory, GetDependencies.EditorConfig)));
        }
    }
}
