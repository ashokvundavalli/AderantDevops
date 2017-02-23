using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Replaces project references inside a .csproj file with file references.
    /// Used for workflow templates that won't have there dependent projects alongside them (they will be resolved from the ExpertShare).
    /// </summary>
    public sealed class ReplaceProjectReferences : Task {
        
        [Required]
        public ITaskItem[] ProjectFileNames { get; set; }

        /// <summary>
        /// Executes this task.
        /// </summary>
        public override bool Execute() {
            foreach (var projectFileName in ProjectFileNames) {
                XElement doc = XElement.Load(projectFileName.ItemSpec);
                ReplaceProjectReferencesInXml(doc);
                doc.Save(projectFileName.ItemSpec);
            }
            return true;
        }

        /// <summary>
        /// Replaces the project references in the <paramref name="doc"/>.
        /// </summary>
        internal static void ReplaceProjectReferencesInXml(XElement doc) {
            XNamespace ns = doc.GetDefaultNamespace();

            List <XElement> projectReferences = doc.Descendants(ns + "ProjectReference").ToList();
            if (!projectReferences.Any()) {
                return;
            }
            
            var assemblyNames = projectReferences.Descendants(ns + "Name").Select(n => n.Value);
            var parentItemGroup = projectReferences.First().Parent;

            if (parentItemGroup == null) {
                throw new XmlException("No parent ItemGroup for the references.");
            }

            projectReferences.Remove();

            foreach (string assemblyName in assemblyNames) {
                XElement newReference = new XElement(ns + "Reference", new XAttribute("Include", assemblyName), new XElement(ns + "Private", "False"));
                parentItemGroup.Add(newReference);
            }

        }
    }
}