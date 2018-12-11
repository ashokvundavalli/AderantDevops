﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public sealed class GenerateBuildPlan : BuildOperationContextTask {

        [Required]
        public string ModulesDirectory { get; set; }

        /// <summary>
        /// Gets or sets the build plan project file.
        /// That is the file that represents the tasks to perform in this build.
        /// </summary>
        [Required]
        public string BuildPlan { get; set; }

        /// <summary>
        /// Gets or sets the targets file which performs the build orchestration
        /// That is the file that represents the coordination tasks for a build instance.
        /// </summary>
        [Required]
        public string GroupExecutionFile { get; set; }

        /// <summary>
        /// Gets or sets the before project file.
        /// That is the file that specifies prologue tasks to execute for each solution.
        /// </summary>
        [Required]
        public string BeforeProjectFile { get; set; }

        /// <summary>
        /// Gets or sets the after project file.
        /// That is the file that specifies epilogue tasks to execute for each solution.
        /// </summary>
        [Required]
        public string AfterProjectFile { get; set; }

        /// <summary>
        /// Gets or sets the project which can define properties to inject into each build group execution.
        /// </summary>
        public string CommonProjectFile { get; set; }

        public string ConfigurationToBuild { get; set; }


        /// <summary>
        /// Gets or sets the make files.
        /// This eventually becomes additional directories to traverse into for cases when projects do not exist in the directory
        /// yet there are build targets that need to be executed.
        /// </summary>
        /// <value>The make files.</value>
        public string[] MakeFiles { get; set; }

        [Output]
        public string[] DirectoriesInBuild { get; set; }

        [Output]
        public string[] ImpactedTestProjects { get; set; }

        public string[] ExcludePaths { get; set; } = new string[0];

        /// <summary>
        /// Gets or sets the extensibility files.
        /// These files contain instructions to influence the build.
        /// </summary>
        public string[] ExtensibilityFiles { get; set; }

        public override bool ExecuteTask() {
            ExecuteCore(Context);
            return !Log.HasLoggedErrors;
        }

        private void ExecuteCore(BuildOperationContext context) {
            // TODO: keep this shim?
            context.BuildRoot = ModulesDirectory;

            if (context.Switches.Resume) {
                if (File.Exists(BuildPlan)) {
                    // When resuming we need to reconstruct the TrackedProjects collection.
                    // This is typically done in the sequencer but since we aren't entering that code path
                    // we need to load the existing set of known projects from our previous plan
                    RebuildFromExistingPlan(BuildPlan);
                    return;
                }
            }

            ExtensibilityImposition extensibilityImposition = null;
            if (ExtensibilityFiles != null) {
                extensibilityImposition = ExtensibilityController.GetExtensibilityImposition(ModulesDirectory, ExtensibilityFiles);
            }

            var projectTree = ProjectTree.CreateDefaultImplementation(new BuildTaskLogger(Log));

            var jobFiles = new OrchestrationFiles {
                BeforeProjectFile = BeforeProjectFile,
                AfterProjectFile = AfterProjectFile,
                GroupExecutionFile = GroupExecutionFile,
                CommonProjectFile = CommonProjectFile,
                ExtensibilityImposition = extensibilityImposition,
                MakeFiles = FilterMakeFiles(),
                BuildPlan = BuildPlan,
            };

            var analysisContext = CreateAnalysisContext();

            context.ConfigurationToBuild = new ConfigurationToBuild(ConfigurationToBuild);

            var buildPlan = projectTree.ComputeBuildPlan(context, analysisContext, PipelineService, jobFiles).Result;
            if (buildPlan.DirectoriesInBuild != null) {
                DirectoriesInBuild = buildPlan.DirectoriesInBuild.ToArray();
            }

            var element = buildPlan.CreateXml();

            var settings = new XmlWriterSettings {
                Encoding = Encoding.UTF8,
                CloseOutput = true,
                NewLineOnAttributes = true,
                IndentChars = "  ",
                Indent = true
            };

            using (var writer = XmlWriter.Create(Path.Combine(ModulesDirectory, BuildPlan), settings)) {
                element.WriteTo(writer);
            }

            ImpactedTestProjects = projectTree.LoadedConfiguredProjects.Where(proj => proj.AreTestsImpacted)
                .Select(proj => proj.GetOutputAssemblyWithExtension()).ToArray();

            PipelineService.Publish(context);
        }

        private List<string> FilterMakeFiles() {
            var files = new List<string>();

            if (MakeFiles != null) {
                foreach (var file in MakeFiles) {
                    bool skip = false;

                    if (ExcludePaths != null) {
                        if (ExcludePaths.Any(pattern => file.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)) {
                            skip = true;
                        }
                    }

                    if (!skip) {
                        Log.LogMessage(MessageImportance.Normal, "Make file: " + file);
                        files.Add(file);
                    }
                }
            }

            return files;
        }

        private void RebuildFromExistingPlan(string planFile) {
            var planLoader = new ExistingPlanLoader();
            DirectoriesInBuild = planLoader.LoadPlan(planFile, PipelineService).ToArray();
        }

        private AnalysisContext CreateAnalysisContext() {
            ErrorUtilities.IsNotNull(Context.BuildSystemDirectory, nameof(Context.BuildSystemDirectory));

            var paths = ExcludePaths.ToList();

            if (!Context.BuildSystemDirectory.Contains("TestResults")) {
                paths.Add(Context.BuildSystemDirectory);
            }

            // Add in the paths passed in by "bm -Exclude ..."
            if (Context.Exclude != null) {
                paths.AddRange(Context.Exclude.ToList());
            }

            var analysisContext = new AnalysisContext {
                ExcludePaths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };

            return analysisContext;
        }
    }

}