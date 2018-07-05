using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.DependencyAnalysis {
    [TestClass]
    [DeploymentItem("DependencyAnalysis\\Resources", "Resources")]
    public class ProjectTreeTests {
        private IProjectTree projectTree;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            this.projectTree = ProjectTree.CreateDefaultImplementation();
        }

        [TestMethod]
        public void Container_provides_default_services_to_tree() {
            Assert.IsNotNull(projectTree.Services.FileSystem);
        }

        [TestMethod]
        public async Task LoadProjectsAsync_sets_LoadedUnconfiguredProjects() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true);

            Assert.AreEqual(5, projectTree.LoadedUnconfiguredProjects.Count);
        }

        [TestMethod]
        public async Task AssemblyReference_captures_hint_path() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true);

            ConfiguredProject configuredProject = projectTree.LoadedUnconfiguredProjects.First(p => p.ProjectGuid == new Guid("{E0E257CE-8CD9-4D58-9C08-6CB6B9A87B92}"))
                .LoadConfiguredProject();

            var servicesAssemblyReferences = configuredProject.Services.AssemblyReferences;
            var unresolvedAssemblyReferences = servicesAssemblyReferences.GetUnresolvedReferences();

            Assert.IsNotNull(unresolvedAssemblyReferences);
            CollectionAssert.Contains(unresolvedAssemblyReferences.Select(s => s.GetHintPath()).ToArray(), @"..\..\ModuleA\Foo\bin\Debug\Foo.dll");
        }

        [TestMethod]
        public async Task BuildDependencyModel_sets_IncludeInBuild() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true);

            await projectTree.CollectBuildDependencies(new BuildDependenciesCollector());

            Assert.IsTrue(projectTree.LoadedConfiguredProjects.All(s => s.IncludeInBuild));
        }

        [TestMethod]
        public async Task Dependency_sorting() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true);

            var collector = new BuildDependenciesCollector();
            await projectTree.CollectBuildDependencies(collector);

            DependencyGraph graph = projectTree.CreateBuildDependencyGraph(collector);
            var dependencyOrder2 = graph.GetDependencyOrder();

            var projects = dependencyOrder2.OfType<ConfiguredProject>().ToList();

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

        private void AssertSequence(string[] sequence, params IEnumerable<string>[] solutions) {
            foreach (var solution in solutions) {
                if (solution.SequenceEqual(sequence, StringComparer.OrdinalIgnoreCase)) {
                    return;
                }
            }

            Assert.Fail("Sequence: " + string.Join(" ", sequence) + " is not expected");
        }
    }

    [TestClass]
    [DeploymentItem("DependencyAnalysis\\Resources", "Resources")]
    public class BuildPipelineTests {
        private IProjectTree projectTree;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            this.projectTree = ProjectTree.CreateDefaultImplementation();
        }

        [TestMethod]
        public async Task Pipeline() {
            await projectTree.LoadProjects(TestContext.DeploymentDirectory, true);

            var collector = new BuildDependenciesCollector();
            await projectTree.CollectBuildDependencies(collector);

            DependencyGraph graph = projectTree.CreateBuildDependencyGraph(collector);

            var pipeline = BuildPipeline.CreateDefaultImplementation();
            pipeline.GenerateTargets(graph);
        }
    }
}
