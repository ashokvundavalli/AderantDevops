using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Model;
using Aderant.Build.MSBuild;
using Aderant.Build.PipelineService;
using Aderant.Build.ProjectSystem;
using Project = Aderant.Build.MSBuild.Project;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Represents a dynamic MSBuild project which will build a set of projects in dependency order and in parallel
    /// </summary>
    internal class BuildPlanGenerator {
        private const string PropertiesKey = "Properties";
        private const string BuildGroupId = "BuildGroupId";
        private static readonly char[] newLineArray = Environment.NewLine.ToCharArray();
        private readonly IFileSystem2 fileSystem;
        private readonly HashSet<string> observedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, PropertyList> solutionPropertyLists = new Dictionary<string, PropertyList>(StringComparer.OrdinalIgnoreCase);
        private string[] commandLineArgs;
        
        public BuildPlanGenerator(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        public Project GenerateProject(List<List<IDependable>> projectGroups, OrchestrationFiles orchestrationFiles, string buildFrom) {
            CaptureCommandLine();

            Project project = new Project();

            // Create a list of call targets for each build
            Target afterCompile = new Target("AfterCompile");

            bool buildFromHere = string.IsNullOrEmpty(buildFrom);

            int buildGroupCount = 0;

            List<MSBuildProjectElement> groups = new List<MSBuildProjectElement>();

            var dashes = new string('—', 20);

            this.observedProjects.UnionWith(projectGroups.SelectMany(s => s).OfType<ConfiguredProject>().Select(s => s.SolutionRoot));

            for (int i = 0; i < projectGroups.Count; i++) {
                buildGroupCount++;

                List<IDependable> projectGroup = projectGroups[i];

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
                        Properties = PropertyList.CreatePropertyString(
                            "BuildPlanFile=$(MSBuildThisFileFullPath)",
                            $"{BuildGroupId}={buildGroupCount}",
                            "TotalNumberOfBuildGroups=$(TotalNumberOfBuildGroups)",
                            "BuildInParallel=$(BuildInParallel)")
                    });

                project.Add(build);

                // e.g <Target Name="AfterCompile" DependsOnTargets="Build0;...n">;
                afterCompile.DependsOnTargets.Add(new Target(build.Name));
            }

            project.Add(
                new PropertyGroup(
                    new Dictionary<string, string> {
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

        private IEnumerable<ItemGroupItem> CreateItemGroupMembers(string beforeProjectFile, string afterProjectFile, List<IDependable> projectGroup, int buildGroup) {
            return projectGroup.Select(
                studioProject => {
                    SetUseCommonOutputDirectory(projectGroup.OfType<ConfiguredProject>());

                    var item = GenerateItem(beforeProjectFile, afterProjectFile, buildGroup, studioProject);

                    if (item != null) {
                        item.Condition = $"('$(ResumeGroupId)' == '') Or ('{buildGroup}' >= '$(ResumeGroupId)')";
                        return item;
                    }

                    return null;
                }
            );
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

                            if (!configuredProject.IsTestProject) {
                                configuredProject.UseCommonOutputDirectory = true;
                            }
                        }
                    }
                }
            }
        }

        private ItemGroupItem GenerateItem(string beforeProjectFile, string afterProjectFile, int buildGroup, IDependable studioProject) {
            Guid itemId = Guid.NewGuid();
            
            PropertyList propertyList = new PropertyList();

            // there are two new ways to pass properties in item metadata, Properties and AdditionalProperties. 
            // The difference can be confusing and very problematic if used incorrectly.
            // The difference is that if you specify properties using the Properties metadata then any properties defined using the Properties attribute 
            // on the MSBuild Task will be ignored. 
            // In contrast to that if you use the AdditionalProperties metadata then both values will be used, with a preference going to the AdditionalProperties values.

            ConfiguredProject visualStudioProject = studioProject as ConfiguredProject;

            if (visualStudioProject != null) {
                if (!visualStudioProject.IncludeInBuild) {
                    return null;
                }

                propertyList = AddSolutionConfigurationProperties(visualStudioProject, propertyList);
                propertyList["Id"] = itemId.ToString("D");

                if (solutionPropertyLists.ContainsKey(propertyList["SolutionRoot"])) {
                    foreach (KeyValuePair<string, string> keyValuePair in solutionPropertyLists[propertyList["SolutionRoot"]]) {
                        if (!propertyList.ContainsKey(keyValuePair.Key)) {
                            propertyList.Add(keyValuePair.Key, keyValuePair.Value);
                        }
                    }
                }

                ItemGroupItem project = new ItemGroupItem(visualStudioProject.FullPath) {
                    ["Id"] = itemId.ToString("D"),
                    [BuildGroupId] = buildGroup.ToString(CultureInfo.InvariantCulture),
                    ["Configuration"] = visualStudioProject.BuildConfiguration.ConfigurationName,
                    ["Platform"] = visualStudioProject.BuildConfiguration.PlatformName,
                    ["AdditionalProperties"] = $"Configuration={visualStudioProject.BuildConfiguration.ConfigurationName}; Platform={visualStudioProject.BuildConfiguration.PlatformName}",
                    ["IsWebProject"] = visualStudioProject.IsWebProject.ToString(),
                    // Indicates this file is not part of the build system
                    ["IsProjectFile"] = bool.TrueString,
                };

                TrackProjectItems(project, visualStudioProject);

                if (project["IsWebProject"] == bool.TrueString) {
                    if (!propertyList.ContainsKey("WebPublishPipelineCustomizeTargetFile")) {
                        propertyList.Add("WebPublishPipelineCustomizeTargetFile", "$(BuildScriptsDirectory)Aderant.wpp.targets");
                    }
                }

                project[PropertiesKey] = propertyList.ToString();

                return project;
            }

            DirectoryNode node = studioProject as DirectoryNode;
            if (node != null) {
                string solutionDirectoryPath = node.Directory;

                PropertyList properties;
                if (solutionPropertyLists.ContainsKey(solutionDirectoryPath)) {
                    properties = solutionPropertyLists[solutionDirectoryPath];
                } else {
                    properties = AddBuildProperties(propertyList, fileSystem, solutionDirectoryPath);
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
                    ["ItemId"] = itemId.ToString("D")
                };

                // Perf optimization, we can disable T4 if we haven't seen any projects under this solution path
                if (!observedProjects.Contains(solutionDirectoryPath)) {
                    properties["T4TransformEnabled"] = bool.FalseString;
                }

                properties["ItemId"] = itemId.ToString("D");
                item[PropertiesKey] = properties.ToString();

                return item;
            }

            return null;
        }

        private static void TrackProjectItems(ItemGroupItem project, ConfiguredProject visualStudioProject) {
            TrackedProject.SetPropertiesNeededForTracking(project, visualStudioProject);
        }

        private PropertyList AddBuildProperties(PropertyList propertyList, IFileSystem2 fileSystem, string solutionDirectoryPath) {
            string responseFile = Path.Combine(solutionDirectoryPath, "Build", Path.ChangeExtension(Constants.EntryPointFile, "rsp"));
            if (fileSystem.FileExists(responseFile)) {
                string propertiesText;
                using (StreamReader reader = new StreamReader(fileSystem.OpenFile(responseFile))) {
                    propertiesText = reader.ReadToEnd();
                }

                PropertyList properties = ParseRspContent(propertiesText.Split(newLineArray, StringSplitOptions.None));

                // We want to be able to specify the flavor globally in a build all so remove it from the property set
                properties.TryRemove("BuildFlavor");

                propertyList = ApplyPropertiesFromCommandLineArgs(properties);

                propertyList.Add("SolutionDirectoryPath", PathUtility.EnsureTrailingSlash(solutionDirectoryPath));
            }

            return propertyList;
        }

        internal PropertyList ParseRspContent(string[] responseFileContent) {
            PropertyList propertyList = new PropertyList();

            if (responseFileContent == null || responseFileContent.Length == 0) {
                return propertyList;
            }

            foreach (string line in responseFileContent) {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) {
                    continue;
                }

                if (line.IndexOf("/p:", StringComparison.OrdinalIgnoreCase) >= 0) {
                    string[] split = line.Replace("\"", "").Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);

                    propertyList.Add(split[0].Substring(3, split[0].Length - 3), split[1]);
                }
            }

            return propertyList;
        }

        /// <summary>
        /// We want to be able to specify options and have these properties act as global variables.
        /// Typically this happens automatically but since we provide the RSP properties explicitly to the MSBuild tasks
        /// within the pipeline we need to evict properties from the RSP that would nullify the command line values
        /// </summary>
        private PropertyList ApplyPropertiesFromCommandLineArgs(PropertyList propertyList) {
            foreach (string argument in commandLineArgs) {
                string[] arg = argument.Replace("\"", "").Split(new[] { '=' }, 2);

                string key = arg[0].Substring(3, arg[0].Length - 3);

                if (propertyList.ContainsKey(key)) {
                    // Here we take the command line arg and apply it, this overwrites whatever the RSP defined
                    propertyList[key] = arg[1];
                }
            }

            return propertyList;
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
    }

   

}
