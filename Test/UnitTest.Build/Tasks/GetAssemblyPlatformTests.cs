using System;
using System.Linq;
using System.Reflection;
using Aderant.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Tasks {
    [TestClass]
    public class GetAssemblyPlatformTests {
        [TestMethod]
        public void DetermineCiStatusNullProperty() {
            Assert.IsFalse(AssemblyInspector.DetermineCiStatus(Assembly.GetExecutingAssembly().CustomAttributes.ToArray()).Item1);
            Assert.IsNull(AssemblyInspector.DetermineCiStatus(Assembly.GetExecutingAssembly().CustomAttributes.ToArray()).Item2);
        }
    }
}
