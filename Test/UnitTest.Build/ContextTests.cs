using Aderant.Build;
using Aderant.Build.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ContextTests {

        [TestMethod]
        public void Service_is_singleton() {

            var ctx = new Context();
            FileSystemService fileSystemService = ctx.CreateService<FileSystemService>();

            Assert.IsNotNull(fileSystemService);
        }
    }
}
