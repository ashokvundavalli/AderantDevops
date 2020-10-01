using Aderant.Build;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class CopyFilesTests {

        [TestMethod]
        public void CopyFiles_handles_hardlinks() {
            var copyFiles = new Aderant.Build.Tasks.CopyFiles(new Moq.Mock<IFileSystem>().Object) {
                BuildEngine = new Mock<IBuildEngine>()
                    .As<IBuildEngine3>()
                    .As<IBuildEngine4>()
                    .Object,
                SourceFiles = new ITaskItem[] {new TaskItem("C:\\Temp\\abc.foo")},
                DestinationFolder = new TaskItem("D:\\Bar"),
                UseHardlinks = true
            };
            
            copyFiles.Execute();

            Assert.AreEqual(1, copyFiles.DestinationFiles.Length);
            Assert.AreEqual("D:\\Bar\\abc.foo", copyFiles.DestinationFiles[0].ItemSpec);
        }
    }
}