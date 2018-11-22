using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem {
    internal class SolutionConfigurationContentsGenerator {
        private readonly ConfigurationToBuild configurationToBuild;

        public SolutionConfigurationContentsGenerator(ConfigurationToBuild configurationToBuild) {
            this.configurationToBuild = configurationToBuild;
        }

        public string CreateSolutionProject(IEnumerable<ProjectInSolution> projectsInSolutions) {
            var sb = new StringBuilder(1024);

            XmlWriterSettings settings = new XmlWriterSettings {
                Indent = true,
                OmitXmlDeclaration = true
            };

            using (XmlWriter xmlWriter = XmlWriter.Create(sb, settings)) {
                xmlWriter.WriteStartElement("SolutionConfiguration");

                foreach (var project in projectsInSolutions) {
                    ProjectConfigurationInSolution projectConfigurationInSolution;
                    if (project.ProjectConfigurations.TryGetValue(configurationToBuild.FullName, out projectConfigurationInSolution)) {
                        xmlWriter.WriteStartElement("ProjectConfiguration");
                        xmlWriter.WriteAttributeString("Project", project.ProjectGuid);
                        xmlWriter.WriteAttributeString("AbsolutePath", project.AbsolutePath);
                        xmlWriter.WriteAttributeString("BuildProjectInSolution", projectConfigurationInSolution.IncludeInBuild.ToString());

                        xmlWriter.WriteString(projectConfigurationInSolution.FullName);
                        xmlWriter.WriteEndElement();
                    }
                }

                xmlWriter.WriteEndElement();
            }

            return sb.ToString();
        }
    }
}