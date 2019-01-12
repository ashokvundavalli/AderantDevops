using System;
using System.Reflection;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AssemblyMetadata("TestRun:DeployMissingReferences", "true")]

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class AssemblyInspectorTests {

        [TestMethod]
        public void Can_find_assembly_deployment_attribute() {
            var inspector = new AssemblyInspector();

            AssemblyName[] referencesToFind;
            ProcessorArchitecture[] referenceArchitectures;

            ImageFileMachine imageFileMachine;
            Exception exception;
            inspector.Inspect(typeof(AssemblyInspectorTests).Assembly.Location, out referencesToFind, out referenceArchitectures, out imageFileMachine, out exception);

            Assert.IsNotNull(referencesToFind);
            Assert.IsNull(referenceArchitectures, "No dependencies provided so no scanning should occur");
        }
    }

}
