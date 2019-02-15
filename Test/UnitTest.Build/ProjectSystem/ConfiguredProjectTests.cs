using System;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.ProjectSystem {
    [TestClass]
    public class ConfiguredProjectTests {

        /// <summary>
        /// Ensures that the property memoization does not bleed across project instances.
        /// </summary>
        [TestMethod]
        public void Property_memoization_returns_values_for_project() {
            var tree = new Moq.Mock<IProjectTree>();

            var cfg1 = new ConfiguredProject(tree.Object);
            cfg1.Initialize(new Lazy<ProjectRootElement>(() => {
                    var element = ProjectRootElement.Create();
                    ProjectPropertyGroupElement propertyGroup = element.AddPropertyGroup();
                    propertyGroup.AddProperty("ProjectTypeGuids", "{3AC096D0-A1C2-E12C-1390-A8335801FDAB};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
                    return element;
                }), "");

            var cfg2 = new ConfiguredProject(tree.Object);
            cfg2.Initialize(new Lazy<ProjectRootElement>(() => {
                var element = ProjectRootElement.Create();
                ProjectPropertyGroupElement propertyGroup = element.AddPropertyGroup();
                propertyGroup.AddProperty("ProjectTypeGuids", "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
                return element;
            }), "");

            Assert.AreEqual(2, cfg1.ProjectTypeGuids.Count);
            Assert.AreEqual(1, cfg2.ProjectTypeGuids.Count);

            Assert.AreEqual(2, cfg1.ProjectTypeGuids.Count);
            Assert.AreEqual(1, cfg2.ProjectTypeGuids.Count);
        }
    }
}