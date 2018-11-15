using Aderant.Build;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyResolver {
    [TestClass]
    public class PackageManagerTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void The_same_name_is_allowed_in_different_groups() {
            var fs = new Moq.Mock<IFileSystem2>();
            fs.Setup(s => s.Root).Returns(TestContext.DeploymentDirectory);
            var packageManager = new PaketPackageManager(TestContext.DeploymentDirectory, fs.Object, new NullLogger());

            packageManager.Add(new [] {
                DependencyRequirement.Create("Foo", "Main"),
                DependencyRequirement.Create("Foo", "Bar"),
            });

            var items1 = packageManager.GetDependencies("Main");
            var items2 = packageManager.GetDependencies("Bar");

            Assert.AreEqual(1, items1.Count);
            Assert.AreEqual(1, items2.Count);

        }
    }
}