﻿using System;
using System.Collections.Generic;
using Aderant.Build;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace UnitTest.Build {
    [TestClass]
    public class ResolverRequestTests {

        [TestMethod]
        public void ResolverRequest_NoExistingDependency() {
            IDependencyRequirement requirement = DependencyRequirement.Create("a", Constants.MainDependencyGroup);
            var resolverRequest = new ResolverRequest(null, "Foo");
            Assert.AreEqual(0, resolverRequest.dependencies.Count);
            var result = resolverRequest.GetOrAdd(requirement);
            Assert.AreEqual(1, resolverRequest.dependencies.Count);
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void ResolverRequest_ExistingDependency() {
            IDependencyRequirement requirement = DependencyRequirement.Create("a", Constants.MainDependencyGroup);
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
                DependencyRequirement.Create("a", Constants.MainDependencyGroup),
                DependencyRequirement.Create("b", Constants.MainDependencyGroup)
            };

            var resolverRequest = new ResolverRequest(null, "Foo");
            Assert.AreEqual(0, resolverRequest.dependencies.Count);

            for (int i = 0; i < requirements.Length; i++) {
                var result = resolverRequest.GetOrAdd(requirements[i]);
                Assert.AreEqual(i + 1, resolverRequest.dependencies.Count);
                Assert.IsNotNull(result);
            }
        }

        [TestMethod]
        public void Resolve_considers_group_when_checking_for_existing_item() {
            IDependencyRequirement[] requirements = {
                DependencyRequirement.Create("a", "Bar"),
                DependencyRequirement.Create("b", "Foo")
            };

            var resolverRequest = new ResolverRequest(null, "Foo");
            Assert.AreEqual(0, resolverRequest.dependencies.Count);

            var result1 = resolverRequest.GetOrAdd(requirements[0]);
            var result2 = resolverRequest.GetOrAdd(requirements[1]);

            Assert.AreEqual(result1.Item.Group, "Bar");
            Assert.AreEqual(result2.Item.Group, "Foo");
        }
    }
}