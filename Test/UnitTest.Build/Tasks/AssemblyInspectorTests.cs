using System.Reflection;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AssemblyMetadata("TestRun:DeployMissingReferences", "true")]

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class AssemblyInspectorTests {

        [TestMethod]
        public void Can_find_assembly_attribute() {
            var inspector =  new AssemblyInspector();

            AssemblyName[] referencesToFind;
            inspector.Inspect(typeof(AssemblyInspectorTests).Assembly.Location, out referencesToFind);

            Assert.IsNotNull(referencesToFind);
        }
    }
}
