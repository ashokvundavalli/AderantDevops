using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks.ReadAssemblyInfo {

    [TestClass]
    public class ReadAssemblyInfoTests {

        [TestMethod]
        public void Can_parse_file_with_attributes() {
            var readInfo = new Aderant.Build.Tasks.ReadAssemblyInfo();
            readInfo.ParseCSharpCode(Resources.AssemblyInfo);

            Assert.IsNotNull(readInfo.AssemblyFileVersion);
            Assert.IsNotNull(readInfo.AssemblyVersion);
        }
    }
}
