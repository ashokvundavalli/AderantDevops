using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build {

    internal static class BuildInfrastructureHelper {

        internal static string PathToBuildScriptsFromModules = Path.Combine("Build.Infrastructure", "Src", "Build");


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
                        toolsVersion.SetValue("12.0");
                    }
                }

                var element = project.Descendants(ns + "Import").FirstOrDefault(elm => {
                    XAttribute attribute = elm.Attribute("Project");
                    if (attribute != null) {
                        var value = attribute.Value;
                        return value.StartsWith(@"\\") && value.EndsWith(Providers.PathHelper.PathToModuleBuild);
                    }
                    return false;
                });

                if (element != null) {
                    // \\na.aderant.com\expertsuite\releases\802x\Build.Infrastructure\Src\Build\ModuleBuild.proj
                    var projectElement = element.Attribute("Project");

                    string branch = Providers.PathHelper.GetBranch(branchDropLocation);
                    int pos = branchDropLocation.IndexOf(branch, StringComparison.OrdinalIgnoreCase);
                    string dropLocationRoot = branchDropLocation.Substring(0, pos);

                    if (projectElement != null) {
                        if (projectElement.Value.IndexOf("$(BranchName)", StringComparison.OrdinalIgnoreCase) < 0) {
                            projectElement.Value = Path.Combine(Path.Combine(dropLocationRoot, "$(BranchName)"), Providers.PathHelper.PathToModuleBuild);
                        }
                    }

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