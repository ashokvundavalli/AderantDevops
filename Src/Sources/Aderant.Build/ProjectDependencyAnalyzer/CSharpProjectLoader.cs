using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.ProjectDependencyAnalyzer;
using Microsoft.Build.BuildEngine;

namespace Aderant.Build.Tasks.BuildTime.ProjectDependencyAnalyzer {
    internal static class WellKnownProjectTypeGuids {
        public static string[] WebProjectGuids = new[] {
            "{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}",
            "{603C0E0B-DB56-11DC-BE95-000D561079B0}",
            "{F85E285D-A4E0-4152-9332-AB1D724D3325}",
            "{E53F8FEA-EAE0-44A6-8774-FFD645390401}",
            "{E3E379DF-F4C6-4180-9B81-6769533ABE47}",
            "{349C5851-65DF-11DA-9384-00065B846F21}",
        };
    }

    /// <summary>
    /// This class provides .csproj parsing capabilities
    /// </summary>
    internal class CSharpProjectLoader {
        /// <summary>
        /// Parses the visual studio project and return the resulting
        /// </summary>
        /// <returns>
        /// An instance of the class <see cref="VisualStudioProject"/> representing the project
        /// </returns>
        public VisualStudioProject Parse(string visualStudioProjectPath) {
            if (!string.IsNullOrEmpty(visualStudioProjectPath) && !visualStudioProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("The project path must be a CSharp project", nameof(visualStudioProjectPath));
            }

            XDocument project;
            var xproject = GetProjectAsXDocument(visualStudioProjectPath, out project);

            if (xproject != null && xproject.Root != null) {
                var root = xproject.Root;

                var projectInfo = (from item in root.Elements("PropertyGroup")
                    where !item.HasAttributes && item.Element("ProjectGuid") != null &&
                          //root.Elements("ItemGroup").Elements("Compile") != null &&
                          //root.Elements("ItemGroup").Elements("Compile").Any() &&
                          item.Element("RootNamespace") != null && item.Element("AssemblyName") != null && item.Element("OutputType") != null
                    select new {
                        RootNamespace = item.Element("RootNamespace")?.Value,
                        AssemblyName = item.Element("AssemblyName")?.Value,
                        ProjectGuid = Guid.Parse(item.Element("ProjectGuid")?.Value ?? string.Empty),
                        OutputType = item.Element("OutputType")?.Value,
                        Path = visualStudioProjectPath,
                        ProjectTypeGuids = ParseProjectTypeGuids(item),
                    }).FirstOrDefault();

                if (projectInfo != null) {
                    var result = new VisualStudioProject(project, projectInfo.ProjectGuid, projectInfo.AssemblyName, projectInfo.RootNamespace, projectInfo.Path) { IsWebProject = IsWebBuild(xproject) };

                    foreach (string reference in FindFileReferences(root)) {
                        result.DependsOn.Add(new AssemblyRef(reference));
                    }

                    foreach (var reference in FindProjectReferences(root)) {
                        result.DependsOn.Add(reference);
                    }

                    if (!string.IsNullOrEmpty(projectInfo.ProjectTypeGuids)) {
                        var projectTypeGuids = projectInfo.ProjectTypeGuids.Split(';');

                        if (!result.IsWebProject) {
                            result.IsWebProject = projectTypeGuids.Intersect(WellKnownProjectTypeGuids.WebProjectGuids, StringComparer.OrdinalIgnoreCase).Any();
                        }
                        result.IsTestProject = projectInfo.ProjectTypeGuids.Contains("{3AC096D0-A1C2-E12C-1390-A8335801FDAB}") || result.DependsOn.Any(r => r.Name == "Microsoft.VisualStudio.QualityTools.UnitTestFramework");
                    }

                    result.OutputType = projectInfo.OutputType;
                    return result;
                }

                throw new InvalidProjectFileException($"Visual Studio project file: {visualStudioProjectPath} is invalid.");
            }

            return null;
        }

        private static string ParseProjectTypeGuids(XElement item) {
            if (item.Element("ProjectTypeGuids") != null) {
                return item.Element("ProjectTypeGuids").Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Parses the visual studio project and return the resulting
        /// </summary>
        /// <param name="ceilingDirectories"></param>
        /// <param name="visualStudioProjectPath">Path to the Visual Studio Project to parse</param>
        /// <param name="result">The parsed project if supported, null otherwise</param>
        /// <returns>
        /// <c>true</c> if the parser was able to load this Visual Studio Project, <c>false</c> otherwise
        /// </returns>
        public bool TryParse(ICollection<string> ceilingDirectories, string visualStudioProjectPath, out VisualStudioProject result) {
            try {
                result = Parse(visualStudioProjectPath);

                if (result == null) {
                    return false;
                }
                return true;
            } catch {
                // Ignore the error and return false
                result = null;
            }

            return false;
        }

        /// <summary>
        /// Get the visual studio project as a <see cref="XDocument"/> instance, without xml namespaces
        /// </summary>
        /// <returns>
        /// The <see cref="XDocument"/> object
        /// </returns>
        private XDocument GetProjectAsXDocument(string visualStudioProjectPath, out XDocument rawDocument) {
            XDocument xproject = XDocument.Load(visualStudioProjectPath);
            rawDocument = new XDocument(xproject);

            if (xproject.Root != null) {
                foreach (XElement e in xproject.Root.DescendantsAndSelf()) {
                    if (e.Name.Namespace != XNamespace.None)
                        e.Name = XNamespace.None.GetName(e.Name.LocalName);

                    if (e.Attributes().Any(a => a.IsNamespaceDeclaration || a.Name.Namespace != XNamespace.None))
                        e.ReplaceAttributes(e.Attributes().Select(a => a.IsNamespaceDeclaration ? null : a.Name.Namespace != XNamespace.None ? new XAttribute(XNamespace.None.GetName(a.Name.LocalName), a.Value) : a));
                }
            }
          
            return xproject;
        }

        /// <summary>
        /// Finds project references in the current project
        /// </summary>
        /// <param name="xproject">
        /// The <see cref="XDocument"/> from which to extract information
        /// </param>
        /// <returns>
        /// The list of assembly names that are file references
        /// </returns>
        private static IEnumerable<ProjectRef> FindProjectReferences(XContainer xproject) {
            var projectReferences = xproject.Elements("ItemGroup").Elements("ProjectReference");

            foreach (var reference in projectReferences) {
                // The name of the reference doesn't necessarily represent the output assembly of the referenced project
                yield return new ProjectRef(reference.Element("Name").Value.Split(',')[0]) {
                    ProjectGuid = GetGuid(reference)
                };
            }
        }

        private static Guid? GetGuid(XElement reference) {
            XElement element = reference.Descendants("Project").SingleOrDefault();

            Guid result;
            if (Guid.TryParse(element.Value, out result)) {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Finds file references in the current project
        /// </summary>
        /// <param name="xproject">
        /// The <see cref="XDocument"/> from which to extract information
        /// </param>
        /// <returns>
        /// The list of assembly names that are file references
        /// </returns>
        private static IEnumerable<string> FindFileReferences(XContainer xproject) {
            var references = xproject.Elements("ItemGroup").Elements("Reference").Select(item => item.Attribute("Include").Value.Split(',')[0]);

            return references.ToList();
        }

        /// <summary>
        /// Check if the project contained in the <see cref="XDocument"/> is a web project
        /// </summary>
        /// <param name="xproject">The <see cref="XDocument"/> loaded with the project file</param>
        /// <returns>
        /// <c>True</c> if the project is a web project, <c>false</c> otherwise
        /// </returns>
        private static bool IsWebBuild(XDocument xproject) {
            var isweb = false;
            if (xproject.Root != null) {
                var projectExtensions = xproject.Root.Element("ProjectExtensions");
                if (projectExtensions != null) {
                    var visualStudio = projectExtensions.Element("VisualStudio");
                    if (visualStudio != null) {
                        var flavor = visualStudio.Element("FlavorProperties");
                        if (flavor != null)
                            isweb = flavor.Element("WebProjectProperties") != null;
                    }
                }
            }

            return isweb;
        }
    }

}