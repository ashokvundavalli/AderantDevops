using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.ProjectSystem.StateTracking {

    /// <summary>
    /// Takes a snapshot of the output files of a project.
    /// </summary>
    internal class ProjectOutputSnapshotFactory {
        private readonly ProjectTreeOutputSnapshot outputs;

        public ProjectOutputSnapshotFactory(ProjectTreeOutputSnapshot outputs) {
            this.outputs = outputs;
        }

        public string SourcesDirectory { get; set; }
        public string ProjectFile { get; set; }
        public string[] ProjectOutputs { get; set; }
        public string OutputPath { get; set; }
        public string IntermediateDirectory { get; set; }
        public IReadOnlyCollection<string> ProjectTypeGuids { get; set; }

        /// <summary>
        /// The test type project property group value - if any.
        /// </summary>
        public string TestProjectType { get; set; }

        /// <summary>
        /// The project references item group identities
        /// </summary>
        public string[] References { get; set; }

        public OutputFilesSnapshot TakeSnapshot(Guid projectGuid) {
            string projectFile = ProjectFile;

            if (SourcesDirectory != null && projectFile.StartsWith(SourcesDirectory, StringComparison.OrdinalIgnoreCase)) {
                projectFile = projectFile.Substring(SourcesDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar)
                    .TrimStart(Path.AltDirectorySeparatorChar);
            }

            bool isTestProject = IsTestProject();

            if (!outputs.ContainsKey(projectFile)) {
                var snapshot = outputs[projectFile] = new OutputFilesSnapshot {
                    ProjectFile = projectFile,
                    ProjectGuid = projectGuid,
                    FilesWritten = RemoveIntermediateObjects(ProjectOutputs, IntermediateDirectory),
                    OutputPath = OutputPath,
                    Origin = "ThisBuild",
                    Directory = GetDirectory(projectFile),
                    IsTestProject = isTestProject,
                };

                return snapshot;
            } else {
                ThrowDoubleWrite();
            }

            return null;
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
                    .Distinct(StringComparer.OrdinalIgnoreCase /* Inputs may contain duplicates */)
                    .ToArray();

            return new string[] { };
        }

        private bool IsTestProject() {
            // Here we handle csproj files that do not advertise themselves properly but checking many facets
            bool isTestProject = string.Equals(TestProjectType, "UnitTest", StringComparison.OrdinalIgnoreCase);

            if (ProjectTypeGuids != null && !isTestProject) {
                foreach (var item in ProjectTypeGuids) {
                    Guid guid;
                    if (Guid.TryParse(item, out guid)) {
                        if (guid == WellKnownProjectTypeGuids.TestProject) {
                            return true;
                        }
                    }
                }
            }

            if (References != null) {
                var references = References;
                foreach (string reference in references) {
                    if (reference.StartsWith("Microsoft.VisualStudio.QualityTools.UnitTestFramework")) {
                        return true;
                    }
                }
            }

            return isTestProject;
        }
    }
}
