using System;
using System.Collections.Generic;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build {
    [TestClass]
    public class ResolverRequestTests {
        [TestMethod]
        public void When_root_directory_ends_with_module_name() {
            var request = new ResolverRequest(null, "Foo\\My.Module");
            var actual = request.GetModuleDirectory(new ExpertModule { Name = "My.Module" });

            Assert.AreEqual("Foo\\My.Module", actual);
        }

        [TestMethod]
        public void When_root_directory_does_not_end_with_module_name() {
            var request = new ResolverRequest(null, "Foo");
            var actual = request.GetModuleDirectory(new ExpertModule { Name = "My.Module" });

            Assert.AreEqual("Foo\\My.Module", actual);
        }

        [TestMethod]
        public void SetDependenciesDirectory_sets_dependency_directory() {
            var request = new ResolverRequest(null, "Foo", new ExpertModule { Name = "Foo" }, new ExpertModule { Name = "Bar" });

            request.SetDependenciesDirectory("Baz");

            var actual = request.GetDependenciesDirectory(DependencyRequirement.Create("Foo"));

            Assert.AreEqual("Baz", actual);
        }

        [TestMethod]
        public void SetDependenciesDirectory_sets_dependency_directory2() {
            var fooModule = new ExpertModule { Name = "Foo" };

            var request = new ResolverRequest(null, "Foo", fooModule, new ExpertModule { Name = "Bar" });

            var barDependency = DependencyRequirement.Create("Bar");

            request.AssociateRequirements(fooModule, new[] { barDependency });

            var actual = request.GetDependenciesDirectory(barDependency);

            Assert.AreEqual("Foo\\Dependencies", actual);
        }

        [TestMethod]
        public void ResolverRequest_NoExistingDependency() {
            IDependencyRequirement requirement = DependencyRequirement.Create("a");
            var resolverRequest = new ResolverRequest(null, "Foo");
            Assert.AreEqual(0, resolverRequest.dependencies.Count);
            var result = resolverRequest.GetOrAdd(requirement);
            Assert.AreEqual(1, resolverRequest.dependencies.Count);
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void ResolverRequest_ExistingDependency() {
            IDependencyRequirement requirement = DependencyRequirement.Create("a");
            var resolverRequest = new ResolverRequest(null, "Foo");
            Assert.AreEqual(0, resolverRequest.dependencies.Count);

            for (int i = 0; i < 2; i++) {
                var result = resolverRequest.GetOrAdd(requirement);
                Assert.AreEqual(1, resolverRequest.dependencies.Count);
                Assert.IsNotNull(result);
            }
        }

        [TestMethod]
        public void ResolverRequest_UniqueDependencies() {
            IDependencyRequirement[] requirements = {
                DependencyRequirement.Create("a"),
                DependencyRequirement.Create("b")
            };

            var resolverRequest = new ResolverRequest(null, "Foo");
            Assert.AreEqual(0, resolverRequest.dependencies.Count);

            for (int i = 0; i < requirements.GetLength(0); i++) {
                var result = resolverRequest.GetOrAdd(requirements[i]);
                Assert.AreEqual(i + 1, resolverRequest.dependencies.Count);
                Assert.IsNotNull(result);
            }
        }
    }
}