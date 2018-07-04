using System;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.References;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.DependencyAnalysis {
    [TestClass]
    [DeploymentItem("DependencyAnalysis\\Resources", "Resources")]
    public class ProjectTreeTests {
        private IProjectTree projectTree;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Foo() {
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
            CollectionAssert.Contains(unresolvedAssemblyReferences.Select(s => s.GetHintPath()).ToArray(), @"..\..\ModuleA\ProjectA\bin\Debug\ProjectA.dll");
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

            Assert.IsTrue(collector.UnresolvedReferences.OfType<IUnresolvedAssemblyReference>().Any());
            Assert.IsTrue(collector.UnresolvedReferences.OfType<IUnresolvedBuildDependencyProjectReference>().Any());

            DependencyGraph analyzeBuildDependencies = projectTree.CreateBuildDependencyGraph(collector);
            var dependencyOrder2 = analyzeBuildDependencies.GetDependencyOrder2();

            var projects = dependencyOrder2.OfType<ConfiguredProject>().ToList();

            Assert.AreEqual(5, dependencyOrder2.OfType<ConfiguredProject>().Count());
            Assert.AreEqual(Guid.Parse("{B807F57F-C8DF-4129-9F0A-01B7F7AA2EF0}"), projects[0].ProjectGuid);
        }
    }
}
