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

        /// <summary>
        /// The source tree path to the project file.
        /// </summary>
        public string ProjectFile { get; set; }

        /// <summary>
        /// The files written by this project during the build.
        /// File paths are expected to be project relative.
        /// </summary>
        public string[] FileWrites { get; set; }

        /// <summary>
        /// The output path of the project.
        /// Expected to be project file relative.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// The MSBuild intermediates directory for the project. This is the the compiler scratch pad.
        /// </summary>
        public string[] IntermediateDirectories { get; set; }

        /// <summary>
        /// The type of the project such as C#, Workflow, ASP.NET etc.
        /// </summary>
        public IReadOnlyCollection<string> ProjectTypeGuids { get; set; }

        /// <summary>
        /// The test type project property group value - if any.
        /// </summary>
        public string TestProjectType { get; set; }

        /// <summary>
        /// The project references item group identities
        /// </summary>
        public string[] References { get; set; }

        public ProjectOutputSnapshotBuilder(string sourcesDirectory, string projectFile, string[] fileWrites, string outputPath, string[] intermediateDirectories, IEnumerable<string> projectTypeGuids, string testProjectType, string[] references) {
            SourcesDirectory = sourcesDirectory;
            ProjectFile = projectFile;
            FileWrites = fileWrites;
            OutputPath = outputPath;
            IntermediateDirectories = intermediateDirectories;
            ProjectTypeGuids = projectTypeGuids?.ToList();
            TestProjectType = testProjectType;
            References = references;

            if (ProjectFile.EndsWith(".wixproj", StringComparison.OrdinalIgnoreCase)) {
                var outputName = Path.GetFileNameWithoutExtension(ProjectFile);
                var wixOutputPath = Path.Combine(OutputPath, outputName + ".wixlib");
                var list = FileWrites.ToList();
                list.Add(wixOutputPath);
                FileWrites = list.ToArray();
            }
        }

        public ProjectOutputSnapshot BuildSnapshot(Guid projectGuid) {
            string projectFile = ProjectFile;

            string projectFileFullPath = ProjectFile;

            if (SourcesDirectory != null && projectFile.StartsWith(SourcesDirectory, StringComparison.OrdinalIgnoreCase)) {
                projectFile = PathUtility.TrimLeadingSlashes(projectFile.Substring(SourcesDirectory.Length));
            }

            bool isTestProject = IsTestProject();

            string relativeOutputPath = PathUtility.MakeRelative(Path.GetDirectoryName(projectFileFullPath), OutputPath);

            if (Path.IsPathRooted(relativeOutputPath)) {
                throw new InvalidOperationException($"Project: '{projectFile}' has rooted output path: '{OutputPath}'.");
            }

            var snapshot = new ProjectOutputSnapshot {
                ProjectFile = projectFile,
                ProjectGuid = projectGuid,
                FilesWritten = RemoveIntermediateObjects(CleanFileWrites(FileWrites), IntermediateDirectories),
                OutputPath = relativeOutputPath,
                Origin = "ThisBuild",
                Directory = GetDirectory(SourcesDirectory),
                IsTestProject = isTestProject,
            };

            return snapshot;
        }

        private string[] CleanFileWrites(IReadOnlyList<string> fileWrites) {
            if (fileWrites != null) {
                string[] cleanedFileWrites = new string[fileWrites.Count];
                string projectFileDirectoryName = Path.GetDirectoryName(ProjectFile);

                for (var i = 0; i < fileWrites.Count; i++) {
                    string fileWrite = fileWrites[i];

                    var cleanFileWrite = fileWrite.Replace(@"\\", @"\");

                    // Turns "C:\\B\\516\\2\\s\\Foo\\Bin\\Module\\Notification.zip" into a relative path
                    if (Path.IsPathRooted(cleanFileWrite)) {
                        cleanFileWrite = PathUtility.MakeRelative(projectFileDirectoryName, cleanFileWrite);
                    }

                    cleanedFileWrites[i] = cleanFileWrite;
                }

                return cleanedFileWrites;
            }

            return null;
        }

        private static string GetDirectory(string dir) {
            return Path.GetFileName(dir);
        }

        private static string[] RemoveIntermediateObjects(IReadOnlyList<string> projectOutputs, string[] intermediateDirectories) {
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
            string[] intermediateDirectories,
            IReadOnlyCollection<string> projectTypeGuids = null,
            string testProjectType = null,
            string[] references = null) {

            ErrorUtilities.IsNotNull(sourcesDirectory, nameof(sourcesDirectory));

            var tracker = new ProjectOutputSnapshotBuilder(sourcesDirectory, projectFile, projectOutputs, outputPath, intermediateDirectories,
                projectTypeGuids, testProjectType, references);

            return tracker.BuildSnapshot(projectGuid);
        }
    }
}