using Aderant.Build;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace IntegrationTest.Build.Tasks.CopyFiles {
    [TestClass]
    public class CopyFilesTests {

        [TestMethod]
        public void CopyFiles_handles_hardlinks() {
            var copyFiles = new Aderant.Build.Tasks.CopyFiles(new Moq.Mock<IFileSystem>().Object);
            copyFiles.BuildEngine = new Mock<IBuildEngine>()
                .As<IBuildEngine3>()
                .As<IBuildEngine4>()
                .Object;

            copyFiles.SourceFiles = new ITaskItem[] { new TaskItem("C:\\Temp\\abc.foo") };
            copyFiles.DestinationFolder = new TaskItem("D:\\Bar");
            copyFiles.UseHardlinks = true;

            copyFiles.Execute();

            Assert.AreEqual(1, copyFiles.DestinationFiles.Length);
            Assert.AreEqual("D:\\Bar\\abc.foo", copyFiles.DestinationFiles[0].ItemSpec);
        }
    }
}