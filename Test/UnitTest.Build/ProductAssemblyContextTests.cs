using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class ProductAssemblyContextTests {

        [TestMethod]
        public void Special_character_indicates_root_item() {
            var module = new ExpertModule { Target = "$/Foo;$/Bar" };

            var context = new ProductAssemblyContext();

            Assert.IsTrue(context.RequiresContentProcessing(module));
        }

        [TestMethod]
        public void IsRootItem_true() {
            var module = ExpertModule.Create(XElement.Parse(@"<Module Name=""Aderant.Deployment.Automation"" GetAction=""NuGet"" Target=""$/AutomatedDeployment"" />"));

            var context = new ProductAssemblyContext();

            Assert.IsTrue(context.RequiresContentProcessing(module));
        }

        [TestMethod]
        public void IsRootItem_false() {
            var module = ExpertModule.Create(XElement.Parse(@"<Module Name=""Aderant.Web.Core"" GetAction=""NuGet"" />"));

            var context = new ProductAssemblyContext();

            Assert.IsFalse(context.RequiresContentProcessing(module));
        }

        [TestMethod]
        public void IsRootItem_false_destination_is_product_directory() {
            var module = ExpertModule.Create(XElement.Parse(@"<Module Name=""Aderant.Web.Core"" GetAction=""NuGet"" />"));

            var context = new ProductAssemblyContext();
            context.ProductDirectory = @"C:\a\b";
            
            var resolvePackageRelativeDirectory = context.ResolvePackageRelativeDirectory(module);

            Assert.AreEqual(context.ProductDirectory, resolvePackageRelativeDirectory);
        }


        [TestMethod]
        public void Root_directory_is_substituted_for_replacement_token() {
            var module = new ExpertModule { Target = "$/Foo;$/Bar" };

            var context = new ProductAssemblyContext();
            context.ProductDirectory = "D:\\Foo";

            var destinations = context.ResolvePackageRelativeDestinationDirectories(module).ToList();

            Assert.AreEqual(2, destinations.Count);
            Assert.AreEqual("D:\\Foo", destinations[0]);
            Assert.AreEqual("D:\\Bar", destinations[1]);
        }
    }
}
