using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class DependencyRequirementTests {

        [TestMethod]
        public void When_requirement_comes_from_dependencymanifest_replace_contraint_is_true() {
            var dependencyRequirement = DependencyRequirement.Create(new ExpertModule {
                RepositoryType = RepositoryType.NuGet
            });

            Assert.IsTrue(dependencyRequirement.ReplaceVersionConstraint);
        }
    }
}