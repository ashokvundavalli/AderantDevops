using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.MSBuild;
using Microsoft.Build.Evaluation;
using Project = Microsoft.Build.Evaluation.Project;

namespace Aderant.Build.BuildProcess {

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

            StringWriter writer = new StringWriter();
            project.Save(writer);

            return XElement.Parse(writer.GetStringBuilder().ToString());
        }


        private Project AddBuildProperties(XElement projectDocument) {
            var project = new Microsoft.Build.Evaluation.Project(projectDocument.CreateReader());

            IEnumerable<string> types = project.ItemTypes.Where(type => type.StartsWith("Build"));
            foreach (string type in types) {
                ICollection<ProjectItem> items = project.GetItems(type);

                // ProjectItem in this case represents a path to a TFSBuild.proj
                foreach (ProjectItem projectItem in items) {
                    string value = projectItem.EvaluatedInclude;

                    if (value.EndsWith("TFSBuild.proj")) {
                        DirectoryInfo buildDirectory = Directory.GetParent(value);
                        string responseFile = Path.Combine(buildDirectory.FullName, "TFSBuild.rsp");

                        if (File.Exists(responseFile)) {
                            string[] properties = File.ReadAllLines(responseFile);
                            string singlePropertyLine = CreateSinglePropertyLine(properties);
                            //projectItem.SetMetadata("Properties", singlePropertyLine);
                            projectItem.SetMetadataValue("Properties", singlePropertyLine);
                        }
                    }
                }
            }

            return project;
        }

        private string CreateSinglePropertyLine(string[] properties) {
            IList<string> lines = new List<string>();

            foreach (string property in properties) {
                if (property.StartsWith("/p:")) {
                    string line =
                        property.Substring(property.IndexOf("/p:", StringComparison.Ordinal) + 3)
                                .Replace("\"", "")
                                .Trim();

                    if (!line.StartsWith("BuildInParallel")) {
                        lines.Add(line);
                    }
                }
            }

            return string.Join(";", lines.ToArray());
        }
    }
}