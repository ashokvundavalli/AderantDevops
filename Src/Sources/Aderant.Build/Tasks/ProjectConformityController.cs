using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;

namespace Aderant.Build.Tasks {
    internal class ProjectConformityController {
        public void AddDirProjectIfNecessary(ProjectRootElement element, string projectLocation) {
            var imports = element.Imports.ToList();

            ProjectImportElement csharpImport = imports.FirstOrDefault(x => x.Project.IndexOf("Microsoft.CSharp.targets", StringComparison.OrdinalIgnoreCase) >= 0);

            if (csharpImport == null) {
                return;
            }

            bool elementsUpdated = false;

            // TODO: We can replace CommonBuildProject with Directory.Build.props

            if (!imports.Any(x => x.Project.IndexOf("CommonBuildProject", StringComparison.OrdinalIgnoreCase) >= 0)) {
                ProjectImportElement importElement = element.CreateImportElement(@"$(CommonBuildProject)\dir.proj");
                importElement.Condition = "$(CommonBuildProject) != ''";
                element.InsertAfterChild(importElement, csharpImport);
                elementsUpdated = true;
            }

            if (!element.Properties.Any(x => string.Equals("CommonBuildProject", x.Name))) {
                element.AddProperty("CommonBuildProject", "$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), 'dir.proj'))");
                elementsUpdated = true;
            }

            if (elementsUpdated && !string.IsNullOrEmpty(projectLocation)) {
                element.Save(projectLocation, Encoding.UTF8);
            }
        }
    }
}
