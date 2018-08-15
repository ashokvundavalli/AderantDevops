using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
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
    }
}
