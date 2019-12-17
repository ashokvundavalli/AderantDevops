using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.ServerImage {
    [TestClass]
    public class DependencyGenerationTests {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void GeneratePaketFiles() {
            string[] sources = new string[] {
                "http://packages.ap.aderant.com/packages/nuget"
            };

            string[,] dependencies = new string[,] {
                { "Aderant.Deployment.Internal", ">=", "11.0.0", "build" }
            };

            StringBuilder paketDependenciesContent = new StringBuilder();

            foreach (string source in sources) {
                paketDependenciesContent.AppendFormat("source {0}", source);
                paketDependenciesContent.AppendLine();
            }

            paketDependenciesContent.AppendLine();

            for (int i = 0; i < dependencies.GetLength(0); i++) {
                paketDependenciesContent.AppendFormat("nuget {0} {1} {2}-{3}", dependencies[i, 0], dependencies[i, 1], dependencies[i, 2], dependencies[i, 3]);
                paketDependenciesContent.AppendLine();
            }

            string paketDependenciesResult = paketDependenciesContent.ToString();

            File.WriteAllText("path", paketDependenciesContent.ToString());
        }
    }
}
