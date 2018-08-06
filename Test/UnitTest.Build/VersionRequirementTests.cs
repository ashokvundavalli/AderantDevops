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

        [TestMethod]
        public void AppendConstraintTest() {
            VersionRequirement a = new VersionRequirement {
                ConstraintExpression = " 1.5.3 ci "
            };

            Assert.AreEqual("= 1.5.3 ci", a.ConstraintExpression);
        }

        [TestMethod]
        [ExpectedException(typeof(System.InvalidOperationException))]
        public void InvalidConstraintTest() {
            new VersionRequirement {
                ConstraintExpression = "build ci rc unstable",
            };
        }
    }
}
