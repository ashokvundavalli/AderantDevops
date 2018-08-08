using Aderant.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ContextTests {

        [TestMethod]
        public void Service_is_returned_from_GetService() {
            var ctx = new BuildOperationContext();
            PhysicalFileSystem service1 = ctx.GetService<PhysicalFileSystem>();

            Assert.IsNotNull(service1);
        }

        [TestMethod]
        public void GetService_creates_service_from_type_name() {
            var ctx = new BuildOperationContext();
            var service = ctx.GetService("Aderant.Build.Services.FileSystemService");

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(PhysicalFileSystem));
        }

        [TestMethod]
        public void GetService_creates_service_from_conditional_export() {
            var ctx = new BuildOperationContext();
            var service = ctx.GetService(typeof(IFileSystem2).FullName);

            Assert.IsNotNull(service);
            Assert.IsInstanceOfType(service, typeof(PhysicalFileSystem));
        }

        [TestMethod]
        public void BuildOptions_can_be_set() {
            var ctx = new BuildOperationContext();
            BuildSwitches switches = ctx.Switches;

            switches.Downstream = true;

            ctx.Switches = switches;

            Assert.AreEqual(true, ctx.Switches.Downstream);
        }
    }
}
