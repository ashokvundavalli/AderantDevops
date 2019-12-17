using System.IO;
using System.Xml.Linq;
using Aderant.Build.ProjectSystem;
using Aderant.Build.ProjectSystem.SolutionParser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.MSBuild {

    [TestClass]
    [DeploymentItem(@"MSBuild\SolutionConfigurationContentsGeneratorTests\Sample.sln")]
    public class SolutionConfigurationContentsGeneratorTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CreateSolutionProject_creates_metaproject_xml() {
            ParseResult result = new SolutionFileParser().Parse(Path.Combine(TestContext.DeploymentDirectory, "Sample.sln"));

            var generator = new SolutionConfigurationContentsGenerator(ConfigurationToBuild.Default);
            string project = generator.CreateSolutionProject(result.ProjectsInOrder);

            Assert.IsNotNull(project);
            XElement.Parse(project);
        }
    }
}