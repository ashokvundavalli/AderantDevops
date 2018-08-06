using Aderant.Build.DependencyResolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class VersionRequirementEqualityTest {
        [TestMethod]
        public void When_versions_match_instances_are_equal() {
            var a = new VersionRequirement {
                AssemblyVersion = "1",
            };

            var b = new VersionRequirement {
                AssemblyVersion = "1",
            };

            Assert.AreEqual(a, b);
        }

        [TestMethod]
        [Ignore]
        public void When_instances_are_not_memberwise_identical_instances_are_not_equal() {
            var a = new VersionRequirement {
                AssemblyVersion = "1",
                ConstraintExpression = "Foo",
            };

            var b = new VersionRequirement {
                AssemblyVersion = "1",
            };

            Assert.AreNotEqual(a, b);
        }
    }
}