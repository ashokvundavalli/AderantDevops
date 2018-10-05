﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aderant.Build.ProjectSystem.StateTracking {

    /// <summary>
    /// Takes a snapshot of the output files of a project.
    /// </summary>
    internal class ProjectOutputSnapshotBuilder {

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

        public string ArtifactStagingDirectory { get; set; }

        public ProjectOutputSnapshot BuildSnapshot(Guid projectGuid) {
            string projectFile = ProjectFile;

            if (SourcesDirectory != null && projectFile.StartsWith(SourcesDirectory, StringComparison.OrdinalIgnoreCase)) {
                projectFile = projectFile.Substring(SourcesDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar)
                    .TrimStart(Path.AltDirectorySeparatorChar);
            }

            bool isTestProject = IsTestProject();

            var snapshot = new ProjectOutputSnapshot {
                AbsoluteProjectFile = ProjectFile,
                ProjectFile = projectFile,
                ProjectGuid = projectGuid,
                FilesWritten = RemoveIntermediateObjects(ProjectOutputs, new[] { IntermediateDirectory, ArtifactStagingDirectory }),
                OutputPath = OutputPath,
                Origin = "ThisBuild",
                Directory = GetDirectory(projectFile),
                IsTestProject = isTestProject,
            };

            return snapshot;
        }

        private static string GetDirectory(string projectFile) {
            return projectFile.Split(Path.DirectorySeparatorChar)[0];
        }

        private static string[] RemoveIntermediateObjects(string[] projectOutputs, string[] intermediateDirectories) {
            if (projectOutputs != null) {
                return projectOutputs
                    .Where(
                        filePath => {
                            foreach (var intermediateDirectory in intermediateDirectories) {
                                if (string.IsNullOrWhiteSpace(intermediateDirectory)) {
                                    continue;
                                }

                                if (filePath.IndexOf(intermediateDirectory, StringComparison.OrdinalIgnoreCase) != -1) {
                                    return false;
                                }
                            }

                            return true;
                        })
                    .OrderBy(filePath => filePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase /* Inputs may contain duplicates */)
                    .ToArray();
            }

            return new string[] { };
        }

        private bool IsTestProject() {
            // Here we handle csproj files that do not advertise themselves properly by checking other facets
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

        internal static ProjectOutputSnapshot RecordProjectOutputs(
            Guid projectGuid,
            string sourcesDirectory,
            string projectFile,
            string[] projectOutputs,
            string outputPath,
            string intermediateDirectory,
            IReadOnlyCollection<string> projectTypeGuids = null,
            string testProjectType = null,
            string[] references = null) {

            ErrorUtilities.IsNotNull(sourcesDirectory, nameof(sourcesDirectory));

            var tracker = new ProjectOutputSnapshotBuilder {
                SourcesDirectory = sourcesDirectory,
                ProjectFile = projectFile,
                ProjectOutputs = projectOutputs,
                OutputPath = outputPath,
                IntermediateDirectory = intermediateDirectory,
                ProjectTypeGuids = projectTypeGuids,
                TestProjectType = testProjectType,
                References = references,
            };

            return tracker.BuildSnapshot(projectGuid);
        }
    }
}
