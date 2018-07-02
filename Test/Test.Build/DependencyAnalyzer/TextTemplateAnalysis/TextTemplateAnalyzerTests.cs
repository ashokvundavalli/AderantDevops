using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTest.Build.DependencyAnalyzer.TextTemplateAnalysis {

    [TestClass]
    public class TextTemplateAnalyzerTests {

        [TestMethod]
        public void Extract_assembly_reference() {

            var analyzer = new TextTemplateAnalyzer();

            TextTemplateAnalysisResult result = analyzer.Analyze(new StringReader(Resources.SimpleTextTemplate));

            Assert.AreEqual(1, result.AssemblyReferences.Count);
            Assert.AreEqual("System.Core", result.AssemblyReferences.First());
        }
    }
}
