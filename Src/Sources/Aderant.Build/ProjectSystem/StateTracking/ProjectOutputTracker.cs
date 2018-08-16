using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.ProjectSystem.StateTracking {
    internal class ProjectOutputTracker {
        private readonly ProjectOutputCollection outputs;

        public ProjectOutputTracker(ProjectOutputCollection outputs) {
            this.outputs = outputs;
        }

        public string SourcesDirectory { get; set; }
        public string ProjectFile { get; set; }
        public string[] ProjectOutputs { get; set; }
        public string OutputPath { get; set; }
        public string IntermediateDirectory { get; set; }
        public IReadOnlyCollection<string> ProjectTypeGuids { get; set; }
        public string TestProjectType { get; set; }

        public void Track() {
            string projectFile = ProjectFile;

            if (SourcesDirectory != null && projectFile.StartsWith(SourcesDirectory, StringComparison.OrdinalIgnoreCase)) {
                projectFile = projectFile.Substring(SourcesDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar)
                    .TrimStart(Path.AltDirectorySeparatorChar);
            }

            bool isTestProject = IsTestProject();

            if (!outputs.ContainsKey(projectFile)) {
                outputs[projectFile] = new ProjectOutputs {
                    FilesWritten = RemoveIntermediateObjects(ProjectOutputs, IntermediateDirectory),
                    OutputPath = OutputPath,
                    Origin = "ThisBuild",
                    Directory = GetDirectory(projectFile),
                    IsTestProject = isTestProject
                };
            } else {
                ThrowDoubleWrite();
            }
        }

        private static string GetDirectory(string projectFile) {
            return projectFile.Split(Path.DirectorySeparatorChar)[0];
        }

        private static void ThrowDoubleWrite() {
            throw new InvalidOperationException("Possible double write detected");
        }

        private static string[] RemoveIntermediateObjects(string[] projectOutputs, string path) {
            if (projectOutputs != null)
                return projectOutputs
                    .Where(item => item.IndexOf(path, StringComparison.OrdinalIgnoreCase) == -1)
                    .OrderBy(filePath => filePath)
                    .ToArray();

            return new string[] { };
        }

        private bool IsTestProject() {
            bool isTestProject = string.Equals(TestProjectType, "UnitTest", StringComparison.OrdinalIgnoreCase);

            if (!isTestProject) {
                if (ProjectTypeGuids != null) {
                    foreach (var item in ProjectTypeGuids) {
                        Guid guid;
                        if (Guid.TryParse(item, out guid)) {
                            if (guid == WellKnownProjectTypeGuids.TestProject) {
                                return true;
                            }
                        }
                    }
                }
            }

            return isTestProject;
        }
    }
}
