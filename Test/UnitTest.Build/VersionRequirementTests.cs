using Aderant.Build.DependencyResolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class VersionRequirementTests {
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
        public void When_instances_are_not_memberwise_identical_instances_are_not_equal() {
            var a = new VersionRequirement {
                AssemblyVersion = "1",
                ConstraintExpression = "> 1",
            };

            var b = new VersionRequirement {
                AssemblyVersion = "1",
            };

            Assert.AreNotEqual(a, b);
        }

        [TestMethod]
        public void Null_inequality() {
            var a = new VersionRequirement {
                AssemblyVersion = "1",
            };
            
            Assert.AreNotEqual(null, a);
            Assert.AreNotEqual(a, null);
        }

        [TestMethod]
        public void Null_equality() {
            var dependencyRequirement = DependencyRequirement.Create("Foo", "");

            Assert.AreEqual(null, dependencyRequirement.VersionRequirement);
            Assert.AreEqual(dependencyRequirement.VersionRequirement, null);
        }
    }
}
