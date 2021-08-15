using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Aderant.Build.MSBuild;
using Microsoft.Build.Definition;
using Project = Microsoft.Build.Evaluation.Project;

namespace Aderant.Build.DependencyAnalyzer {
    internal sealed class ParallelBuildVisitor : TargetXmlEmitter {
        public override XElement GetXml() {
            XElement projectDocument = base.GetXml();

            XElement afterCompileElement =
                projectDocument.Elements()
                               .FirstOrDefault(
                                   elm => elm.FirstAttribute != null && elm.FirstAttribute.Value == "AfterCompile");
            if (afterCompileElement != null) {
                afterCompileElement.AddBeforeSelf(
                    new XComment(
                        "Do not use CallTarget here - only DependsOnTargets target. It is possible to trigger an MS Build bug due to the call graph complexity: \"MSB0001:Internal MSBuild Error: We should have already left any legacy call target scopes\" "));
                afterCompileElement.AddBeforeSelf(
                    new XComment(
                        "This target defines the end point of the project, each build level will be called from 0 .. n before this executes"));
            }

            ValidateProject(projectDocument);

            return projectDocument;
        }

        private static void ValidateProject(XElement projectDocument) {
            // Pass the generated XML to MSBuild for validation
            using (XmlReader reader = projectDocument.CreateReader()) {
                Project.FromXmlReader(reader, new ProjectOptions() { });
            }
        }
    }
}
