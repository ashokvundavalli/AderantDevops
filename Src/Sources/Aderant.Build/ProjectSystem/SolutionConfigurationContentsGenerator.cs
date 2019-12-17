using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem {
    /// <summary>
    /// Some steps in the standard MS pipeline rely on a SLN 'metaproj' to work.
    /// This takes care of emitting a document those pipeline tasks need.
    /// </summary>
    /// <remarks>
    /// Creates the the "CurrentSolutionConfigurationContents" object that
    /// MSBuild uses when building from a solution. Unfortunately the build engine doesn't let us build just projects
    /// due to the way that AssignProjectConfiguration in Microsoft.Common.CurrentVersion.targets assumes that you are coming from a solution
    /// and attempts to assign platforms and targets to project references, even if you are not building them.
    /// </remarks>
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