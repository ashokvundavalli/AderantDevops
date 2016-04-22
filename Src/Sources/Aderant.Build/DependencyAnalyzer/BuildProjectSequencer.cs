using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Create a MS Build project containing an item group of builds sorted and ordered according to critical path
    /// </summary>
    internal sealed class BuildProjectSequencer {
        private XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
        private static string directorySeparatorChar = Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Sequences the builds within the given branch.
        /// </summary>
        /// <param name="provider">The provider.</param>
        /// <returns></returns>
        public IEnumerable<Build> SequenceBuilds(IModuleProvider provider) {
            DependencyBuilder builder = new DependencyBuilder(provider);

            return builder.GetTree(true);
        }

        /// <summary>
        /// Creates the MS Build build project document from the specified builds.
        /// </summary>
        /// <param name="existingProject">The existing project. Can be null.</param>
        /// <param name="builds">The builds.</param>
        /// <returns></returns>
        public string CreateBuildDocument(TextReader existingProject, IEnumerable<Build> builds) {
            XDocument project;

            if (existingProject != null) {
                project = XDocument.Load(existingProject);
            } else {
                project = new XDocument(new XElement(ns + "Project", new XAttribute("ToolsVersion", 14.0)));
            }

            project.Declaration = new XDeclaration("1.0", "utf-8", "yes");;

            project.Descendants(ns + "ItemGroup").Remove();

            XElement itemGroup = CreateModuleItemGroup(builds.GroupBy(g => g.Order));

            project.Root.Add(itemGroup);

            return CreateProjectText(project);
        }

        private static string CreateProjectText(XDocument project) {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;
            settings.ConformanceLevel = ConformanceLevel.Document;
            settings.IndentChars = "    ";
            settings.Indent = true;

            using (MemoryStream stream = new MemoryStream()) {
                using (XmlWriter writer = XmlWriter.Create(stream, settings)) {
                    project.Save(writer);
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private XElement CreateModuleItemGroup(IEnumerable<IGrouping<int, Build>> builds) {
            XElement group = new XElement(ns + "ItemGroup");

            foreach (IGrouping<int, Build> grouping in builds) {
                group.Add(new XComment("Build Group: " + grouping.Key));

                foreach (Build build in grouping) {
                    foreach (ExpertModule module in build.Modules) {
                        XElement entry = new XElement(ns + "Modules", new XAttribute("Include", "$(SolutionRoot)" + directorySeparatorChar + module.Name));
                        group.Add(entry);
                    }
                }
            }
            return group;
        }

        /// <summary>
        /// Sequences the builds provided by the specified <see cref="IModuleProvider" /> instance and checks out the project file for edits.
        /// </summary>
        /// <param name="workspace">The source control provider.</param>
        /// <param name="project">The project.</param>
        /// <param name="provider">The provider.</param>
        /// <returns>
        /// The updated project text.
        /// </returns>
        internal static string CreateOrUpdateBuildSequence(ITeamFoundationWorkspace workspace, string project, IModuleProvider provider) {
            var sequencer = new BuildProjectSequencer();
            IEnumerable<Build> builds = sequencer.SequenceBuilds(provider);

            if (!File.Exists(project)) {
                workspace.PendAdd(project);

                return sequencer.CreateBuildDocument(null, builds);
            }

            workspace.PendEdit(project);

            using (StreamReader reader = new StreamReader(project)) {
                return sequencer.CreateBuildDocument(reader, builds);
            }
        }
    }
}