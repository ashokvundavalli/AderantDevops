using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyResolver.Resolvers;

namespace Aderant.Build.DependencyResolver {
    internal static class ResolverSettingsReader {
        /// <summary>
        /// Reads an XML fragment that contains resolver settings.
        /// </summary>
        /// <param name="resolverWorkflow">The workflow object to configure.</param>
        /// <param name="configurationXml">The configuration blob used to configure the workflow.</param>
        /// <param name="pathToFile">The on disk file path of the configuration document. Used to set the <paramref name="sharedDependencyDirectory"/> if the setting is relative to the file.</param>
        /// <param name="buildRoot">The build root, used to set the shared dependency directory path.</param>
        /// <param name="sharedDependencyDirectory">The shared dependency directory from the configuration file</param>
        public static void ReadResolverSettings(ResolverWorkflow resolverWorkflow, XDocument configurationXml, string pathToFile, string buildRoot, out string sharedDependencyDirectory) {
            ErrorUtilities.IsNotNull(buildRoot, nameof(buildRoot));

            sharedDependencyDirectory = null;

            SetSharedDependencyDirectory(configurationXml, pathToFile, buildRoot, ref sharedDependencyDirectory);
            if (sharedDependencyDirectory != null) {
                sharedDependencyDirectory = PathUtility.EnsureTrailingSlash(sharedDependencyDirectory);
            }

            if (resolverWorkflow != null) {
                var replicationEnabledValue = configurationXml.Descendants("DependencyReplicationEnabled").FirstOrDefault()?.Value;

                if (string.IsNullOrWhiteSpace(replicationEnabledValue)) {
                    // Enable replication by default for backwards compatibility.
                    resolverWorkflow.UseReplication(true);
                } else {
                    resolverWorkflow.UseReplication(Convert.ToBoolean(replicationEnabledValue));
                }

                InitializeResolvers(configurationXml, resolverWorkflow);
            }
        }

        private static void SetSharedDependencyDirectory(XDocument configurationXml, string pathToFile, string buildRoot, ref string sharedDependencyDirectory) {
            var sharedDependencyDirectoryElement = configurationXml.Descendants("DependenciesDirectory").FirstOrDefault();
            if (sharedDependencyDirectoryElement != null) {
                sharedDependencyDirectory = sharedDependencyDirectoryElement.Value;

                XAttribute attribute = sharedDependencyDirectoryElement.Attribute("RelativeTo");
                if (attribute == null) {
                    // No attribute, assume its relative to the repository root (default)
                    sharedDependencyDirectory = Path.Combine(buildRoot, sharedDependencyDirectory);
                } else {
                    if (string.Equals(attribute.Name.LocalName, "ThisFile", StringComparison.OrdinalIgnoreCase)) {
                        var thisFilePath = Path.GetDirectoryName(pathToFile);
                        sharedDependencyDirectory = Path.Combine(thisFilePath, sharedDependencyDirectoryElement.Value);
                    }
                }
            }
        }

        private static void InitializeResolvers(XDocument xml, ResolverWorkflow resolverWorkflow) {
            XElement element;
            if (xml.Root.Name.LocalName.Equals("DependencyResolvers")) {
                element = xml.Root;
            } else {
                element = xml.Root.Element("DependencyResolvers");
            }

            if (element != null) {
                ParseProperties(element, resolverWorkflow);
            }
        }

        private static void ParseProperties(XElement dependencyResolverElement, ResolverWorkflow workflow) {
            var resolvers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in dependencyResolverElement.Descendants()) {
                if (element.Name.LocalName == nameof(NupkgResolver)) {
                    resolvers.Add(element.Name.LocalName);

                    var validationElement = element.Descendants("ValidatePackageConstraints").FirstOrDefault();

                    if (validationElement != null && bool.TryParse(validationElement.Value, out bool validatePackageConstraints)) {
                        workflow.GetCurrentRequest().ValidatePackageConstraints = validatePackageConstraints;
                    }
                }

                if (element.Name.LocalName == nameof(ExpertModuleResolver)) {
                    resolvers.Add(element.Name.LocalName);
                }
            }

            workflow.WithResolvers(resolvers.ToArray());
        }
    }
}
