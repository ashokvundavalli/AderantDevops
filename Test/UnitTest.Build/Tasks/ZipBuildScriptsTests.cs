using System;
using System.IO;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class ZipBuildScriptsTests {
        public TestContext TestContext { get; set; }
        
        [DeploymentItem("Aderant.Build.dll")]
        [TestMethod]
        public void ZipBuildScriptsCreatesOutput() {
            var testDir = TestContext.TestDir;

            var zipTask = new ZipBuildScripts {TargetDirectory = testDir, BuildAssemblyLocation = Path.Combine(TestContext.TestDeploymentDir, "Aderant.Build.dll")};
            zipTask.Execute();

            var expectedFile = Path.Combine(testDir, "BuildScripts.zip");
            Assert.IsTrue(File.Exists(expectedFile));
        }
    }
}
