using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Model;
using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Represents a dynamic MSBuild project which will build a set of projects in dependency order and in parallel
    /// </summary>
    internal class BuildPlanGenerator {
        private const string PropertiesKey = "Properties";
        private const string BuildGroupId = "BuildGroupId";

        private readonly IFileSystem fileSystem;
        private readonly HashSet<string> observedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, PropertyList> solutionPropertyLists = new Dictionary<string, PropertyList>(StringComparer.OrdinalIgnoreCase);
        private string[] commandLineArgs;

        public BuildPlanGenerator(IFileSystem fileSystem) {
            this.fileSystem = fileSystem;
        }

        private bool IsDesktopBuild { get; set; }

        public string MetaprojectXml { get; set; }

        public event EventHandler<ItemGroupItemMaterializedEventArgs> ItemGroupItemMaterialized;

        public Project GenerateProject(IReadOnlyList<IReadOnlyList<IDependable>> projectGroups, OrchestrationFiles orchestrationFiles, bool desktopBuild, string buildFrom = null) {
	        IsDesktopBuild = desktopBuild;
	        CaptureCommandLine();

            Project project = new Project();

            // Create a list of call targets for each build
            Target afterCompile = new Target("AfterCompile");

            bool buildFromHere = string.IsNullOrEmpty(buildFrom);

            int buildGroupCount = 0;

            List<MSBuildProjectElement> groups = new List<MSBuildProjectElement>();

            var dashes = new string('—', 20);

            this.observedProjects.UnionWith(projectGroups.SelectMany(s => s).OfType<ConfiguredProject>().Select(s => s.SolutionRoot));

            var buildPlanId = Guid.NewGuid().ToString("D");

            for (int i = 0; i < projectGroups.Count; i++) {
                buildGroupCount++;

                IReadOnlyList<IDependable> projectGroup = projectGroups[i];

                // If there are no projects in the item group, no point generating any Xml for this build node
                if (!projectGroup.Any()) {
                    continue;
                }

                if (!buildFromHere) {
                    buildFromHere = projectGroup.OfType<ExpertModule>().Any(m => string.Equals(m.Name, buildFrom, StringComparison.OrdinalIgnoreCase));
                }

                if (!buildFromHere) {
                    continue;
                }

                ItemGroup itemGroup = new ItemGroup(
                    "ProjectsToBuild",
                    CreateItemGroupMembers(
                        orchestrationFiles.BeforeProjectFile,
                        orchestrationFiles.AfterProjectFile,
                        orchestrationFiles.ExtensibilityImposition,
                        projectGroup,
                        buildGroupCount));

                groups.Add(new Comment($" {dashes} BEGIN: {itemGroup.Name} {dashes} "));
                groups.Add(itemGroup);
                groups.Add(new Comment($" {dashes} END: {itemGroup.Name} {dashes} "));

                // e.g. <Target Name="Foo">
                Target build = new Target("Run" + CreateGroupName(buildGroupCount));
                build.Condition = "'$(BuildEnabled)' == 'true'";

                if (buildGroupCount > 0) {
                    var target = project.Elements.OfType<Target>().FirstOrDefault(t => t.Name == CreateGroupName(buildGroupCount - 1));
                    if (target != null) {
                        build.DependsOnTargets.Add(target);
                    }
                }

                build.Add(
                    new MSBuildTask {
                        BuildInParallel = "$(BuildInParallel)",
                        StopOnFirstFailure = true,
                        Projects = orchestrationFiles.GroupExecutionFile,
                        Properties = new PropertyList(new Dictionary<string, string>()) {
                            { "BuildPlanFile", "$(MSBuildThisFileFullPath)" },
                            { $"{BuildGroupId}", $"{buildGroupCount}" },
                            { "TotalNumberOfBuildGroups", "$(TotalNumberOfBuildGroups)" },

                            // Should we use all CPUs?
                            { "BuildInParallel", "$(BuildInParallel)" },

                            // The VS project reference graph, needed for RAR compatibility
                            { "CurrentSolutionConfigurationContents", "$(CurrentSolutionConfigurationContents)" },

                            // A unique id for this plan, could be used to create directories just for this build instance
                            { "BuildPlanId", buildPlanId },
                        }.ToString()
                    });

                project.Add(build);

                // e.g <Target Name="AfterCompile" DependsOnTargets="Build0;...n">;
                afterCompile.DependsOnTargets.Add(new Target(build.Name));
            }

            project.Add(
                new PropertyGroup(
                    new Dictionary<string, string> {
                        { "CurrentSolutionConfigurationContents", MetaprojectXml ?? string.Empty },
                        { "TotalNumberOfBuildGroups", buildGroupCount.ToString(CultureInfo.InvariantCulture) },
                        { "ResumeGroupId", "" }
                    }));

            project.Add(groups);
            project.Add(afterCompile);

            // The target that MSBuild will call into to start the build
            project.DefaultTarget = afterCompile;

            return project;
        }

        private void CaptureCommandLine() {
            this.commandLineArgs = Environment.GetCommandLineArgs();
            // Find all global command line property specifications
            commandLineArgs = commandLineArgs.Where(x => x.StartsWith("/p:", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        private static string CreateGroupName(int buildGroupCount) {
            return "ProjectsToBuild" + buildGroupCount;
        }

        private IEnumerable<ItemGroupItem> CreateItemGroupMembers(string beforeProjectFile, string afterProjectFile, ExtensibilityImposition imposition, IReadOnlyList<IDependable> projectGroup, int buildGroup) {
            SetUseCommonOutputDirectory(projectGroup.OfType<ConfiguredProject>());

            foreach (var studioProject in projectGroup) {
                var item = GenerateItem(beforeProjectFile, afterProjectFile, buildGroup, studioProject, imposition);

                if (item != null) {
                    item.Condition = $"('$(ResumeGroupId)' == '') Or ('{buildGroup}' >= '$(ResumeGroupId)')";
                    yield return item;
                }
            }
        }

        internal static void SetUseCommonOutputDirectory(IEnumerable<ConfiguredProject> projects) {
            var solutionGroups = projects
                .GroupBy(project => project.SolutionRoot, StringComparer.OrdinalIgnoreCase)
                .Select(dirGrouping => dirGrouping.Key);

            foreach (var keys in solutionGroups) {
                var projectsForSolution = projects.Where(p => string.Equals(p.SolutionRoot, keys, StringComparison.OrdinalIgnoreCase));

                var projectsByOutputPath = projectsForSolution.GroupBy(g => g.OutputPath, StringComparer.OrdinalIgnoreCase);

                foreach (var configuredProjects in projectsByOutputPath) {
                    if (configuredProjects.Count() > 1) {
                        foreach (var configuredProject in configuredProjects) {

                            if (!configuredProject.IsTestProject && !configuredProject.IsOfficeProject) {
                                configuredProject.UseCommonOutputDirectory = true;
                            }
                        }
                    }
                }
            }
        }

        private ItemGroupItem GenerateItem(string beforeProjectFile, string afterProjectFile, int buildGroup, IDependable studioProject, ExtensibilityImposition imposition) {
            Guid projectInstanceId = Guid.NewGuid();

            PropertyList propertiesForProjectInstance = new PropertyList();

            // There are two new ways to pass properties in item metadata, Properties and AdditionalProperties.
            // The difference can be confusing and very problematic if used incorrectly.
            // The difference is that if you specify properties using the Properties metadata then any properties defined using the Properties attribute
            // on the MSBuild Task will be ignored.
            // In contrast to that if you use the AdditionalProperties metadata then both values will be used, with a preference going to the AdditionalProperties values.

            ConfiguredProject visualStudioProject = studioProject as ConfiguredProject;

            if (visualStudioProject != null) {
                if (!visualStudioProject.IncludeInBuild) {
                    return null;
                }

                if (!visualStudioProject.RequiresBuilding()) {
                    return null;
                }

                var project = SetConfiguredProjectProperties(buildGroup, propertiesForProjectInstance, visualStudioProject, imposition, projectInstanceId);
                return project;
            }

            DirectoryNode node = studioProject as DirectoryNode;
            if (node != null) {
                var item = SetDirectoryProperties(buildGroup, propertiesForProjectInstance, node, beforeProjectFile, afterProjectFile, projectInstanceId);
                return item;
            }

            return null;
        }

        private ItemGroupItem SetConfiguredProjectProperties(int buildGroup, PropertyList propertiesForProjectInstance, ConfiguredProject visualStudioProject, ExtensibilityImposition imposition, Guid projectInstanceId) {
            propertiesForProjectInstance = AddSolutionConfigurationProperties(visualStudioProject, propertiesForProjectInstance);
            propertiesForProjectInstance["Id"] = projectInstanceId.ToString("D");

            if (solutionPropertyLists.ContainsKey(propertiesForProjectInstance["SolutionRoot"])) {
                foreach (KeyValuePair<string, string> keyValuePair in solutionPropertyLists[propertiesForProjectInstance["SolutionRoot"]]) {
                    if (!propertiesForProjectInstance.ContainsKey(keyValuePair.Key)) {
                        propertiesForProjectInstance.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }
            }

            ItemGroupItem project = new ItemGroupItem(visualStudioProject.FullPath) {
                ["Id"] = projectInstanceId.ToString("D"),
                [BuildGroupId] = buildGroup.ToString(CultureInfo.InvariantCulture),
                ["IsWebProject"] = visualStudioProject.IsWebProject.ToString(),
                // Indicates this file is not part of the build system
                ["IsProjectFile"] = bool.TrueString,
            };

            if (imposition != null && imposition.CreateHardLinksForCopyLocal) {
                propertiesForProjectInstance["CreateHardLinksForCopyLocal"] = bool.TrueString;
            }

            if (visualStudioProject.BuildConfiguration != null) {
                project["Configuration"] = visualStudioProject.BuildConfiguration.ConfigurationName;
                project["Platform"] = visualStudioProject.BuildConfiguration.PlatformName;
                project["AdditionalProperties"] = $"Configuration={visualStudioProject.BuildConfiguration.ConfigurationName}; Platform={visualStudioProject.BuildConfiguration.PlatformName}";
            }

            if (visualStudioProject.BuildReason != null && visualStudioProject.BuildReason.Flags.HasFlag(BuildReasonTypes.AlwaysBuild)) {
                project["Targets"] = "Rebuild";
            }

            AddProjectOutputToUpdatePackage(propertiesForProjectInstance, visualStudioProject);

            TrackProjectItems(project, visualStudioProject);

            if (project["IsWebProject"] == bool.TrueString) {
                if (!propertiesForProjectInstance.ContainsKey("WebPublishPipelineCustomizeTargetFile")) {
                    propertiesForProjectInstance.Add("WebPublishPipelineCustomizeTargetFile", "$(BuildScriptsDirectory)Aderant.wpp.targets");
                }
            }

            OnItemGroupItemMaterialized(new ItemGroupItemMaterializedEventArgs(project, propertiesForProjectInstance));

            project[PropertiesKey] = propertiesForProjectInstance.ToString();
            return project;
        }

        private ItemGroupItem SetDirectoryProperties(int buildGroup, PropertyList propertiesForProjectInstance, DirectoryNode node, string beforeProjectFile, string afterProjectFile, Guid projectInstanceId) {

            string solutionDirectoryPath = node.Directory;

            PropertyList properties;
            if (solutionPropertyLists.ContainsKey(solutionDirectoryPath)) {
                properties = solutionPropertyLists[solutionDirectoryPath];
            } else {
                properties = AddBuildProperties(propertiesForProjectInstance, fileSystem, solutionDirectoryPath);
                solutionPropertyLists.Add(solutionDirectoryPath, properties);
            }

            if (!properties.ContainsKey("SolutionRoot")) {
                properties.Add("SolutionRoot", solutionDirectoryPath);
            }

            ItemGroupItem item = new ItemGroupItem(node.IsPostTargets ? afterProjectFile : beforeProjectFile) {
                [BuildGroupId] = buildGroup.ToString(CultureInfo.InvariantCulture),

                // Indicates this file is part of the build system itself
                ["IsPostTargets"] = node.IsPostTargets ? bool.TrueString : bool.FalseString,
                ["IsPreTargets"] = !node.IsPostTargets ? bool.TrueString : bool.FalseString,
                ["IsProjectFile"] = bool.FalseString,
                ["ProjectInstanceId"] = projectInstanceId.ToString("D"),
            };

            if (node.RetrievePrebuilts != null) {
                if (!node.RetrievePrebuilts.Value) {
                    properties["RetrievePrebuilts"] = bool.FalseString;
                }
            }

            // Perf optimization, we can disable T4 if we haven't seen any projects under this solution path
            // Don't do this on desktop to simplify things for developers
            if (!IsDesktopBuild && !node.IsPostTargets && !node.IsBuildingAnyProjects) {
                properties["T4TransformEnabled"] = bool.FalseString;
            }

            properties["ProjectInstanceId"] = projectInstanceId.ToString("D");
            properties["RunUserTargets"] = node.AddedByDependencyAnalysis ? bool.FalseString : bool.TrueString;

            OnItemGroupItemMaterialized(new ItemGroupItemMaterializedEventArgs(item, properties));

            item[PropertiesKey] = properties.ToString();
            return item;
        }

        private static void AddProjectOutputToUpdatePackage(PropertyList propertyList, ConfiguredProject visualStudioProject) {
            bool? hasDirtyFiles = visualStudioProject.DirtyFiles?.Any();
            if (hasDirtyFiles.GetValueOrDefault() || visualStudioProject.BuildReason.Flags.HasFlag(BuildReasonTypes.ProjectChanged)) {
                propertyList["IncludeOutputInUpdatePackage"] = bool.TrueString;
            }
        }

        private static void TrackProjectItems(ItemGroupItem project, ConfiguredProject visualStudioProject) {
            OnDiskProjectInfo.SetPropertiesNeededForTracking(project, visualStudioProject);
        }

        internal PropertyList AddBuildProperties(PropertyList propertiesForProjectInstance, IFileSystem fileSystem, string solutionDirectoryPath) {
            string responseFile = ResponseFileParser.CreatePath(solutionDirectoryPath);
            var parser = new ResponseFileParser(fileSystem);

            var properties = parser.ParseFile(responseFile);
            if (properties != null) {
                // We want to be able to specify the flavor globally in a build all so remove it from the property set
                properties.TryRemove("BuildFlavor");
                propertiesForProjectInstance = ApplyPropertiesFromCommandLineArgs(properties);
            }

            AddDefault("Deterministic", bool.TrueString, propertiesForProjectInstance, properties);
            AddDefault("SolutionDirectoryPath", PathUtility.EnsureTrailingSlash(solutionDirectoryPath), propertiesForProjectInstance, properties);

            return propertiesForProjectInstance;
        }

        /// <summary>
        /// We want to be able to specify options and have these properties act as global variables.
        /// Typically this happens automatically but since we provide the RSP properties explicitly to the MSBuild tasks
        /// within the pipeline we need to evict properties from the RSP that would nullify the command line values
        /// </summary>
        private PropertyList ApplyPropertiesFromCommandLineArgs(PropertyList propertyList) {
            if (commandLineArgs != null) {
                foreach (string argument in commandLineArgs) {
                    string[] arg = argument.Replace("\"", "").Split(new[] { '=' }, 2);

                    string key = arg[0].Substring(3, arg[0].Length - 3);

                    if (propertyList.ContainsKey(key)) {
                        // Here we take the command line arg and apply it, this overwrites whatever the RSP defined
                        propertyList[key] = arg[1];
                    }
                }
            }

            return propertyList;
        }

        private static void AddDefault(string key, string value, PropertyList propertiesForProjectInstance, PropertyList properties) {
            if (properties == null || !properties.ContainsKey(key)) {
                propertiesForProjectInstance.Add(key, value);
            }
        }

        private PropertyList AddSolutionConfigurationProperties(ConfiguredProject visualStudioProject, PropertyList propertyList) {
            propertyList.Add(nameof(ConfiguredProject.SolutionRoot), visualStudioProject.SolutionRoot);

            AddMetaProjectProperties(visualStudioProject, propertyList);

            if (visualStudioProject.UseCommonOutputDirectory) {
                propertyList.Add(nameof(ConfiguredProject.UseCommonOutputDirectory), bool.TrueString);
            }

            return propertyList;
        }

        private static void AddMetaProjectProperties(ConfiguredProject visualStudioProject, PropertyList propertiesList) {
            // MSBuild metaproj compatibility items (from the generated solution.sln.metaproj)
            // The following properties are 'macros' that are available via IDE for
            // pre and post build steps. However, they are not defined when directly building
            // a project from the command line, only when building a solution.
            // A lot of stuff doesn't work if they aren't present (web publishing targets for example) so we add them in as compatibility items
            propertiesList.Add("SolutionDir", visualStudioProject.SolutionRoot);
            propertiesList.Add("SolutionExt", Path.GetExtension(visualStudioProject.SolutionFile));
            propertiesList.Add("SolutionFileName", Path.GetFileName(visualStudioProject.SolutionFile));
            propertiesList.Add("SolutionPath", visualStudioProject.SolutionRoot);
            propertiesList.Add("SolutionName", Path.GetFileNameWithoutExtension(visualStudioProject.SolutionFile));

            propertiesList.Add("BuildingSolutionFile", bool.FalseString);
        }

        protected virtual void OnItemGroupItemMaterialized(ItemGroupItemMaterializedEventArgs e) {
            ItemGroupItemMaterialized?.Invoke(this, e);
        }
    }

    internal class ItemGroupItemMaterializedEventArgs {

        public ItemGroupItemMaterializedEventArgs(ItemGroupItem itemGroupItem, PropertyList properties) {
            ItemGroupItem = itemGroupItem;
            Properties = properties;
        }

        public ItemGroupItem ItemGroupItem { get; }
        public PropertyList Properties { get; }
    }
}