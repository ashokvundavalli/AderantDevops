using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.Tasks {
    internal class ProjectConformityController {
        private readonly IFileSystem2 fs;
        private readonly Project project;

        public ProjectConformityController(IFileSystem2 fs, Project project) {
            this.fs = fs;
            this.project = project;
        }

        public bool AddDirProjectIfNecessary() {
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
            project.ProjectCollection.Dispose();
        }

        public static Project CreateProject(string path) {
            var collection = new ProjectCollection {
                SkipEvaluation = true
            };

            return collection.LoadProject(path);
        }

        public static Project CreateProject(XDocument document) {
            var collection = new ProjectCollection {
                SkipEvaluation = true
            };

            return collection.LoadProject(document.CreateReader());
        }
    }
}