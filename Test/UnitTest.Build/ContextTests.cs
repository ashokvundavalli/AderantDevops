using Aderant.Build;
using Aderant.Build.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ContextTests {

        [TestMethod]
        public void Service_is_returned_from_GetService() {
            var ctx = new Context();
            FileSystemService service1 = ctx.GetService<FileSystemService>();

            Assert.IsNotNull(service1);
        }

        [TestMethod]
        public void Default_ArgumentBuilder_is_MsBuild() {
            var ctx = new Context();
            var service1 = ctx.CreateArgumentBuilder(WellKnownProperties.MsBuild);

            Assert.IsNotNull(service1);
            Assert.IsInstanceOfType(service1, typeof(ComboBuildArgBuilder));
        }

        [TestMethod]
        public void GetService_creates_service_from_type_name() {
            var ctx = new Context();
            var service = ctx.GetService("Aderant.Build.Services.FileSystemService");

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(FileSystemService));
        }

        [TestMethod]
        public void GetService_creates_service_from_supplemental_export() {
            var ctx = new Context();
            var service = ctx.GetService(typeof(IFileSystem).FullName);

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(FileSystemService));
        }


        [TestMethod]
        public void BuildOptions_can_be_set() {
            var ctx = new Context();
            BuildSwitches switches = ctx.Switches;

            switches.Downstream = true;

            ctx.Switches = switches;

            Assert.AreEqual(true, ctx.Switches.Downstream);
        }
    }
}