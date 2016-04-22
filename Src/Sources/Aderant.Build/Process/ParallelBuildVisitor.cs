using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Aderant.Build.MSBuild;
using Microsoft.Build.Evaluation;
using Project = Microsoft.Build.Evaluation.Project;

namespace Aderant.Build.Process {

    internal sealed class ParallelBuildVisitor : BuildElementVisitor {

        public override XElement GetDocument() {
            XElement projectDocument = base.GetDocument();
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

            var project = AddBuildProperties(projectDocument);

            using (StringWriter writer = new StringWriter()) {
                project.Save(writer);
                return XElement.Parse(writer.GetStringBuilder().ToString());
            }
        }

        private Project AddBuildProperties(XElement projectDocument) {

            using (XmlReader reader = projectDocument.CreateReader()) {
                Project project = new Project(reader);

                IEnumerable<string> types = project.ItemTypes.Where(type => type.StartsWith("Build", StringComparison.OrdinalIgnoreCase));
                foreach (string type in types) {
                    ICollection<ProjectItem> items = project.GetItems(type);

                    // ProjectItem in this case represents a path to a TFSBuild.proj
                    foreach (ProjectItem projectItem in items) {
                        string value = projectItem.EvaluatedInclude;

                        if (value.EndsWith("TFSBuild.proj", StringComparison.OrdinalIgnoreCase)) {
                            DirectoryInfo buildDirectory = Directory.GetParent(value);
                            string responseFile = Path.Combine(buildDirectory.FullName, "TFSBuild.rsp");

                            if (File.Exists(responseFile)) {
                                string[] properties = File.ReadAllLines(responseFile);

                                // We want to be able to specify the flavor globally in a build all so remove it from the property set
                                properties = RemoveFlavor(properties);

                                string singlePropertyLine = CreateSinglePropertyLine(properties);

                                if (buildDirectory.Parent != null) {
                                    singlePropertyLine += ";SolutionDirectoryPath=" + buildDirectory.Parent.FullName + @"\";
                                }

                                projectItem.SetMetadataValue("Properties", singlePropertyLine);
                            }
                        }
                    }
                }
                return project;
            }
        }

        private string[] RemoveFlavor(string[] properties) {
            IEnumerable<string> newProperties = properties.Where(p => p.IndexOf("BuildFlavor", StringComparison.InvariantCultureIgnoreCase) == -1);

            return newProperties.ToArray();
        }

        private string CreateSinglePropertyLine(string[] properties) {
            IList<string> lines = new List<string>();

            foreach (string property in properties) {
                if (property.StartsWith("/p:")) {
                    string line =
                        property.Substring(property.IndexOf("/p:", StringComparison.Ordinal) + 3)
                                .Replace("\"", "")
                                .Trim(null);

                    if (!line.StartsWith("BuildInParallel")) {
                        lines.Add(line);
                    }
                }
            }

            return string.Join(";", lines.ToArray());
        }
    }
}