using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer.TextTemplates;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.TextTemplateAnalysis {

    [TestClass]
    public class TextTemplateAnalyzerTests {

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Extract_assembly_references() {
            var analyzer = new TextTemplateAnalyzer();

            using (var reader = new StringReader(Resources.SimpleTextTemplate)) {
                TextTemplateAnalysisResult result = analyzer.Analyze(reader, TestContext.DeploymentDirectory);

                Assert.AreEqual(5, result.AssemblyReferences.Count);
                Assert.AreEqual("System.Core", result.AssemblyReferences.First());
            }
        }

        [TestMethod]
        public void Extract_include_directive() {
            var analyzer = new TextTemplateAnalyzer();

            using (var reader = new StringReader(Resources.TextTemplateWithInclude)) {
                TextTemplateAnalysisResult result = analyzer.Analyze(reader, TestContext.DeploymentDirectory);

                Assert.AreEqual("common.ttinclude", result.Includes[0]);
                Assert.AreEqual(Path.Combine(TestContext.DeploymentDirectory, "common1.ttinclude"), result.Includes[1]);
            }
        }

        [TestMethod]
        public void Extract_custom_processor_directive() {
            var analyzer = new TextTemplateAnalyzer();

            using (var reader = new StringReader(Resources.TextTemplateWithCustomProcessor)) {
                TextTemplateAnalysisResult result = analyzer.Analyze(reader, TestContext.DeploymentDirectory);

                Assert.AreEqual("DomainModelDslDirectiveProcessor", result.CustomProcessors[0]);
            }
        }
    }
}
