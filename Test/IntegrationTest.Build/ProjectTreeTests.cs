using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build {
    [TestClass]
    [DeploymentItem("EndToEnd\\Resources", "Resources")]
    public class ProjectTreeTests {
        private IProjectTree projectTree;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            this.projectTree = ProjectTree.CreateDefaultImplementation(NullLogger.Default);
        }

        [TestMethod]
        public void Container_provides_default_services_to_tree() {
            Assert.IsNotNull(projectTree.Services.FileSystem);
        }

        [TestMethod]
        public async Task LoadProjectsAsync_sets_LoadedUnconfiguredProjects() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true, null);

            Assert.AreEqual(5, projectTree.LoadedUnconfiguredProjects.Count);
        }

        [TestMethod]
        public async Task AssemblyReference_captures_hint_path() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true, null);

            ConfiguredProject configuredProject = projectTree.LoadedUnconfiguredProjects.First(p => p.ProjectGuid == new Guid("{E0E257CE-8CD9-4D58-9C08-6CB6B9A87B92}"))
                .LoadConfiguredProject();

            var servicesAssemblyReferences = configuredProject.Services.AssemblyReferences;
            var unresolvedAssemblyReferences = servicesAssemblyReferences.GetUnresolvedReferences();

            Assert.IsNotNull(unresolvedAssemblyReferences);
            CollectionAssert.Contains(unresolvedAssemblyReferences.Select(s => s.GetHintPath()).ToArray(), @"..\..\ModuleA\Foo\bin\Debug\Foo.dll");
        }

        [TestMethod]
        public async Task BuildDependencyModel_sets_IncludeInBuild() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true, null);

            var collector = new BuildDependenciesCollector();
            collector.ProjectConfiguration = ConfigurationToBuild.Default;
            await projectTree.CollectBuildDependencies(collector);

            Assert.IsTrue(projectTree.LoadedConfiguredProjects.All(s => s.IncludeInBuild));
        }

        [TestMethod]
        public async Task Dependency_sorting() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true, null);

            var collector = new BuildDependenciesCollector {
                ProjectConfiguration = ConfigurationToBuild.Default
            };
            await projectTree.CollectBuildDependencies(collector);

            DependencyGraph graph = projectTree.CreateBuildDependencyGraph(collector);
            var dependencies = graph.GetDependencyOrder();

            var projects = dependencies.OfType<ConfiguredProject>().ToList();

            Assert.AreEqual(5, projects.Count);

            var solution1 = new[] {
                "Foo",
                "Baz",
                "Gaz",
                "Bar",
                "Flob",
            };

            var solution2 = new[] {
                "Foo",
                "Gaz",
                "Baz",
                "Bar",
                "Flob",
            };

            var sequence = projects.Select(s => Path.GetFileNameWithoutExtension(s.FullPath)).ToArray();

            AssertSequence(sequence, solution1, solution2);
        }

        [TestMethod]
        public async Task Project_dependencies_are_discovered() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true, null);

            var collector = new BuildDependenciesCollector {
                ProjectConfiguration = ConfigurationToBuild.Default
            };
            await projectTree.CollectBuildDependencies(collector);

            DependencyGraph graph = projectTree.CreateBuildDependencyGraph(collector);
            var dependencies = graph.GetDependencyOrder();

            var projects = dependencies.OfType<ConfiguredProject>().ToList();

            Assert.AreEqual(5, projects.Count);

            Assert.AreEqual(0, projects[0].GetDependencies().Count);
            Assert.AreEqual(1, projects[1].GetDependencies().Count);
            Assert.AreEqual(1, projects[2].GetDependencies().Count);
            Assert.AreEqual(2, projects[3].GetDependencies().Count);
            Assert.AreEqual(3, projects[4].GetDependencies().Count);

            AssertSequence(
                projects[1].GetDependencies(),
                "Foo");

            AssertSequence(
                projects[2].GetDependencies(),
                "Foo");

            AssertSequence(
                projects[3].GetDependencies(),
                "Foo",
                "Baz");

            AssertSequence(
                projects[4].GetDependencies(),
                "Gaz",
                "Bar",
                "Foo"
            );
        }

        private static void AssertSequence(IEnumerable<IDependable> sequence, params string[] solutions) {
            AssertSequence(sequence.Select(s => s.Id).ToArray(), solutions);
        }

        private static void AssertSequence(string[] sequence, params IEnumerable<string>[] solutions) {
            foreach (var solution in solutions) {
                if (solution.SequenceEqual(sequence, StringComparer.OrdinalIgnoreCase)) {
                    return;
                }
            }

            Assert.Fail("Sequence: " + string.Join(" ", sequence) + " is not expected");
        }
    }

}
