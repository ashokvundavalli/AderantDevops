using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Xml;
using Aderant.Build;
using Aderant.Build.ProjectSystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.ConfiguredProjectTests {
    [TestClass]

    public class ConfiguredProjectTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Load_project_v12_toolset() {
            var project = new UnconfiguredProject(new ProjectTree());
            project.ConfiguredProjectFactory = new ExportFactory<ConfiguredProject>(() => new Tuple<ConfiguredProject, Action>(new ConfiguredProject(new ProjectTree(), new PhysicalFileSystem()), () => { }));
            project.Initialize(XmlReader.Create(new StringReader(Resources.Web_Core)), "");

            var configuredProject = project.LoadConfiguredProject();
            Assert.IsTrue(configuredProject.IsWebProject);
        }

        [TestMethod]
        public void Load_project_v14_toolset() {
            var project = new UnconfiguredProject(new ProjectTree());
            project.ConfiguredProjectFactory = new ExportFactory<ConfiguredProject>(() => new Tuple<ConfiguredProject, Action>(new ConfiguredProject(new ProjectTree(), new PhysicalFileSystem()), () => { }));
            project.Initialize(XmlReader.Create(new StringReader(Resources.Web_PrebillEditor)), "");

            var configuredProject = project.LoadConfiguredProject();
            Assert.IsTrue(configuredProject.IsWebProject);
        }

        [TestMethod]
        [DeploymentItem("ConfiguredProjectTests\\Web.PrebillEditor.csproj")]
        public void Load_project_from_disk() {
            var project = new UnconfiguredProject(new ProjectTree());
            project.ConfiguredProjectFactory = new ExportFactory<ConfiguredProject>(() => new Tuple<ConfiguredProject, Action>(new ConfiguredProject(new ProjectTree(), new PhysicalFileSystem()), () => { }));
            project.Initialize(XmlReader.Create(new StringReader(Resources.Web_PrebillEditor)), Path.Combine(TestContext.DeploymentDirectory, "Web.PrebillEditor.csproj"));

            var configuredProject = project.LoadConfiguredProject();
            Assert.IsTrue(configuredProject.IsWebProject);
        }
    }
}
