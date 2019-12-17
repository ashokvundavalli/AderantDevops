using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace IntegrationTest.Build.Tasks.CopyFiles {
    [TestClass]
    public class CopyFilesTests  {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CopyFiles_handles_hardlinks() {
            var copyFiles = new Aderant.Build.Tasks.CopyFiles();
            copyFiles.BuildEngine = new Mock<IBuildEngine>()
                .As<IBuildEngine3>()
                .As<IBuildEngine4>()
                .Object;

            string testContextDeploymentDirectory = TestContext.DeploymentDirectory;

            var randomFileName1 = Path.Combine(testContextDeploymentDirectory, Path.GetRandomFileName());
            var randomFileName2 = Path.Combine(testContextDeploymentDirectory, Path.GetRandomFileName());

            File.WriteAllText(randomFileName1, "1");

            copyFiles.SourceFiles = new ITaskItem[] { new TaskItem(randomFileName1), };
            copyFiles.DestinationFiles = new ITaskItem[] { new TaskItem(randomFileName2), };
            copyFiles.UseHardlinks = true;

            copyFiles.Execute();

            // Ensure that we don't error on a second iterationZ
            copyFiles.Execute();
        }
    }
}