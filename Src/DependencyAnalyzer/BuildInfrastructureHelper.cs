﻿using System.IO;
using System.Linq;
using System.Management.Automation.Host;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DependencyAnalyzer.Providers;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace DependencyAnalyzer {

    internal static class BuildInfrastructureHelper {

        /// <summary>
        /// Updates the path to module build project within a TFSBuild.proj
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="serverPathToModule">The server path to module.</param>
        /// <param name="branchDropLocation">The branch drop location.</param>
        internal static void UpdatePathToModuleBuildProject(Workspace workspace, string serverPathToModule, string branchDropLocation) {
            string localPath = workspace.TryGetLocalItemForServerItem(serverPathToModule + "/Build/TFSBuild.proj");

            if (File.Exists(localPath)) {
                workspace.PendEdit(localPath);

                string contents = File.ReadAllText(localPath);

                XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
                var project = XDocument.Parse(contents, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

                XAttribute toolsVersion = project.Root.Attribute("ToolsVersion");
                if (toolsVersion != null) {
                    if (toolsVersion.Value == "3.5") {
                        toolsVersion.SetValue("4.0");
                    }
                }

                var element = project.Descendants(ns + "Import").FirstOrDefault(elm => {
                    XAttribute attribute = elm.Attribute("Project");
                    if (attribute != null) {
                        var value = attribute.Value;
                        return value.StartsWith(@"\\") && value.EndsWith(PathHelper.PathToModuleBuild);
                    }
                    return false;
                });

                if (element != null) {
                    string path = string.Format(@"{0}\{1}", branchDropLocation, PathHelper.PathToModuleBuild);

                    element.Attribute("Project").Value = path;

                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Encoding = Encoding.UTF8;
                    settings.ConformanceLevel = ConformanceLevel.Document;
                    settings.IndentChars = "  ";
                    settings.Indent = true;
                    settings.NewLineOnAttributes = true;

                    using (XmlWriter writer = XmlWriter.Create(localPath, settings)) {
                        project.Save(writer);
                    }
                }
            }
        }
    }
}