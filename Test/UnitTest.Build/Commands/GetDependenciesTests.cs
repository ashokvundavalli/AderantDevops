using System;
using System.IO;
using Aderant.Build;
using Aderant.Build.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Build.Helpers;

namespace UnitTest.Build.Commands {
    [TestClass]
    public class GetDependenciesTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Resources\sln.DotSettings")]
        public void ReSharperFileWriteTest() {
            var getDependencies = new GetDependencies(new TextContextLogger(TestContext), new PhysicalFileSystem());

            string destinationDirectory = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());

            getDependencies.CopyReSharperSettings(File.ReadAllText(Path.Combine(TestContext.DeploymentDirectory, GetDependencies.ResharperSettings)), destinationDirectory, TestContext.DeploymentDirectory);

            string destinationFile = Path.Combine(destinationDirectory, GetDependencies.ResharperSettings);

            Assert.IsTrue(File.Exists(destinationFile));

            string content = File.ReadAllText(destinationFile);

            Assert.AreEqual(-1, content.IndexOf(GetDependencies.AbsolutePathToken, StringComparison.Ordinal), $"{nameof(GetDependencies.AbsolutePathToken)} was not replaced.");
            Assert.AreEqual(-1, content.IndexOf(GetDependencies.RelativePathToken, StringComparison.Ordinal), $"{nameof(GetDependencies.RelativePathToken)} was not replaced.");
        }

        [TestMethod]
        [DeploymentItem(@"Resources\.editorconfig")]
        public void HardLinkTest() {
            var getDependencies = new GetDependencies(new TextContextLogger(TestContext), new PhysicalFileSystem());

            string destinationDirectory = Path.Combine(TestContext.DeploymentDirectory, Path.GetRandomFileName());

            Directory.CreateDirectory(destinationDirectory);

            getDependencies.LinkEditorConfigFile(Path.Combine(TestContext.DeploymentDirectory, GetDependencies.EditorConfig), destinationDirectory);

            Assert.IsTrue(File.Exists(Path.Combine(destinationDirectory, GetDependencies.EditorConfig)));
        }
    }
}
