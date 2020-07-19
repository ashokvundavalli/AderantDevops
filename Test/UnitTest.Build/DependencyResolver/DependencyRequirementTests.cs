using Aderant.Build.DependencyResolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.DependencyResolver {
    [TestClass]
    public class DependencyRequirementTests {

        [TestMethod]
        public void AssignGroup_creates_group() {
            var requirement = DependencyRequirement.Create("SomeOtherPackage", "Main2");
            DependencyRequirement.AssignGroup(new[] {requirement }, true);

            IDependencyGroup dependencyGroup = requirement as IDependencyGroup;
            Assert.IsNotNull(dependencyGroup.DependencyGroup);
            Assert.IsTrue(dependencyGroup.DependencyGroup.Strict);
        }
    }
}