using System;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;

namespace Aderant.Build.Tasks {
    internal class ProjectConformityController {
        public void AddDirProjectIfNecessary(ProjectRootElement element, string projectLocation) {
            var imports = element.Imports.ToList();

            ProjectImportElement csharpImport = null;

            foreach (var import in imports) {
                if (import.Project.IndexOf("CommonBuildProject", StringComparison.OrdinalIgnoreCase) >= 0) {
                    return;
                }

                if (import.Project.IndexOf("Microsoft.CSharp.targets", StringComparison.OrdinalIgnoreCase) >= 0) {
                    csharpImport = import;
                }
            }

            if (csharpImport != null) {
                element.AddProperty("CommonBuildProject", "$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), 'dir.proj'))");

                ProjectImportElement importElement = element.CreateImportElement(@"$(CommonBuildProject)\dir.proj");
                importElement.Condition = "$(CommonBuildProject) != ''";
                element.InsertAfterChild(importElement, csharpImport);

                if (!string.IsNullOrEmpty(projectLocation)) {
                    element.Save(projectLocation, Encoding.UTF8);
                }
            }
        }
    }
}
