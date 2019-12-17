using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Xml;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.ConfiguredProjectTests {
    [TestClass]
    public class ConfiguredProjectTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Load_project_v12_toolset() {
            var project = new UnconfiguredProject();
            project.ConfiguredProjectFactory = new ExportFactory<ConfiguredProject>(() => new Tuple<ConfiguredProject, Action>(new ConfiguredProject(new ProjectTree()), () => { }));
            project.Initialize(LoadProjectXml(Resources.Web_Core), "");

            var configuredProject = project.LoadConfiguredProject(null);
            Assert.IsTrue(configuredProject.IsWebProject);
        }

        [TestMethod]
        public void Load_project_v14_toolset() {
            var project = new UnconfiguredProject();
            project.ConfiguredProjectFactory = new ExportFactory<ConfiguredProject>(() => new Tuple<ConfiguredProject, Action>(new ConfiguredProject(new ProjectTree()), () => { }));
            project.Initialize(LoadProjectXml(), "");

            var configuredProject = project.LoadConfiguredProject(null);
            Assert.IsTrue(configuredProject.IsWebProject);
        }

        [TestMethod]
        [DeploymentItem("ConfiguredProjectTests\\Web.PrebillEditor.csproj")]
        public void Load_project_from_disk() {
            var project = new UnconfiguredProject();
            project.ConfiguredProjectFactory = new ExportFactory<ConfiguredProject>(() => new Tuple<ConfiguredProject, Action>(new ConfiguredProject(new ProjectTree()), () => { }));
            project.Initialize(LoadProjectXml(), Path.Combine(TestContext.DeploymentDirectory, "Web.PrebillEditor.csproj"));

            var configuredProject = project.LoadConfiguredProject(null);
            Assert.IsTrue(configuredProject.IsWebProject);
        }

        [TestMethod]
        [DeploymentItem("ConfiguredProjectTests\\Web.PrebillEditor.csproj")]
        public void GetOutputAssemblyWithExtension() {
            var project = new UnconfiguredProject();
            project.ConfiguredProjectFactory = new ExportFactory<ConfiguredProject>(() => new Tuple<ConfiguredProject, Action>(new ConfiguredProject(new ProjectTree()), () => { }));
            project.Initialize(LoadProjectXml(), Path.Combine(TestContext.DeploymentDirectory, "Web.PrebillEditor.csproj"));

            var configuredProject = project.LoadConfiguredProject(null);
            Assert.AreEqual("Web.PrebillEditor.dll", configuredProject.GetOutputAssemblyWithExtension());
        }

        private XmlReader LoadProjectXml(string resource = null) {
            return XmlReader.Create(new StringReader(resource ?? Resources.Web_PrebillEditor));
        }
    }
}
