using System.IO;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyAnalyzer.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.DependencyAnalyzer.CSharpProjectLoaderTests {
    [TestClass]
    [DeploymentItem("DependencyAnalyzer\\CSharpProjectLoaderTests\\", "Resources")]
    public class CSharpProjectLoaderTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CSharpProjectLoader_Parse() {
            CSharpProjectLoader cSharpProjectLoader = new CSharpProjectLoader();

            VisualStudioProject webProject = cSharpProjectLoader.Parse(Resources.Web_Core);
            VisualStudioProject webProject2 = cSharpProjectLoader.Parse(Resources.Web_PrebillEditor);

            Assert.IsTrue(webProject.IsWebProject);
            Assert.IsTrue(webProject2.IsWebProject);
        }

        [TestMethod]
        public void CSharpProjectLoader_Load() {
            CSharpProjectLoader cSharpProjectLoader = new CSharpProjectLoader();

            VisualStudioProject webProject = cSharpProjectLoader.Load(Path.Combine(TestContext.DeploymentDirectory, "Resources\\Web.Core.csproj"));
            
            Assert.IsTrue(webProject.IsWebProject);
        }
    }
}
