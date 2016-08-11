using System;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class ProjectConformityCheck : Microsoft.Build.Utilities.Task {
        
        public ITaskItem Project { get; set; }
        public override bool Execute() {

            var controller = new ProjectConformityController(new PhysicalFileSystem(Project.GetMetadata("Directory")), new Project(Project.ItemSpec));

            if (controller.AddDirProjIfNecessary()) {
                Log.LogWarning("The project file {0} does not have a dir.proj import. One will be added.", Project.ItemSpec);

                controller.Save();
            }

            controller.Unload();

            return !Log.HasLoggedErrors;
        }
    }

    internal class ProjectConformityController {
        private readonly IFileSystem2 fs;
        private readonly Project project;

        public ProjectConformityController(IFileSystem2 fs, Project project) {
            this.fs = fs;
            this.project = project;
        }

        public bool AddDirProjIfNecessary() {
            bool added = false;

            ProjectImportElement import = project.Xml.Imports.FirstOrDefault(p => p.Project.IndexOf("CommonBuildProject", StringComparison.OrdinalIgnoreCase) >= 0);
            if (import == null) {
                ProjectImportElement csharpImport = project.Xml.Imports.FirstOrDefault(i => i.Project.IndexOf("Microsoft.CSharp.targets", StringComparison.OrdinalIgnoreCase) >= 0);
                if (csharpImport != null) {

                    ProjectImportElement importElement = project.Xml.CreateImportElement(@"$(CommonBuildProject)\dir.proj");
                    importElement.Condition = "$(CommonBuildProject) != ''";
                    project.Xml.InsertAfterChild(importElement, csharpImport);

                    added = true;
                }

                project.Xml.AddProperty("CommonBuildProject", "$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), 'dir.proj'))");
            }

            return added;
        }

        public void Save() {
            fs.MakeFileWritable(project.FullPath);
            project.Save();
        }

        public void Unload() {
            project.ProjectCollection.UnloadProject(project);
        }
    }
}
