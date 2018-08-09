﻿using System;
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
    internal class BuildPipeline {
        private const string PropertiesKey = "Properties";
        private const string BuildGroupId = "BuildGroupId";
        private static readonly char[] newLineArray = Environment.NewLine.ToCharArray();
        private readonly IFileSystem2 fileSystem;

        public BuildPipeline(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        public Project GenerateProject(List<List<IDependable>> projectGroups, OrchestrationFiles orchestrationFiles, string buildFrom) {
            Project project = new Project();

            // Create a list of call targets for each build
            Target afterCompile = new Target("AfterCompile");

            bool buildFromHere = string.IsNullOrEmpty(buildFrom);

            int buildGroupCount = 0;

            List<MSBuildProjectElement> groups = new List<MSBuildProjectElement>();

            var dashes = new string('—', 20);

            for (int i = 0; i < projectGroups.Count; i++) {

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
                        buildGroupCount,
                        buildFrom));

                groups.Add(new Comment($" {dashes} BEGIN: {itemGroup.Name} {dashes} "));
                groups.Add(itemGroup);
                groups.Add(new Comment($" {dashes} END: {itemGroup.Name} {dashes} "));

                // e.g. <Target Name="Foo">
                Target build = new Target("Run" + CreateGroupName(buildGroupCount));
                build.Condition = "'$(BuildEnabled)' == 'true'";
                build.DependsOnTargets.Add(new Target("CreateCommonProperties"));

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
                            "InstanceProjectFile=$(MSBuildThisFileFullPath)",
                            $"{BuildGroupId}={buildGroupCount}",
                            "TotalNumberOfBuildGroups=$(TotalNumberOfBuildGroups)",
                            "BuildInParallel=$(BuildInParallel)",
                            "PropertiesForBuildGroup=$(PropertiesForBuildGroup)")
                    });

                project.Add(build);

                // e.g <Target Name="AfterCompile" DependsOnTargets="Build0;...n">;
                afterCompile.DependsOnTargets.Add(new Target(build.Name));

                buildGroupCount++;
            }

            project.Add(
                new PropertyGroup(
                    new Dictionary<string, string> {
                        { "TotalNumberOfBuildGroups", buildGroupCount.ToString(CultureInfo.InvariantCulture) },
                        { "ResumeGroupId", "" }
                    }));
            project.Add(groups);
            project.Add(afterCompile);
            project.Add(new ImportElement { Project = orchestrationFiles.CommonProjectFile });

            // The target that MSBuild will call into to start the build
            project.DefaultTarget = afterCompile;

            return project;
        }

        private static string CreateGroupName(int buildGroupCount) {
            return "ProjectsToBuild" + buildGroupCount;
        }

        private IEnumerable<ItemGroupItem> CreateItemGroupMembers(string beforeProjectFile, string afterProjectFile, List<IDependable> projectGroup, int buildGroup, string buildFrom) {
            return projectGroup.Select(
                studioProject => {
                    var item = GenerateItem(beforeProjectFile, afterProjectFile, buildGroup, studioProject);
                    if (item != null) {
                        item.Condition = $"('$(ResumeGroupId)' == '') Or ('{buildGroup}' >= '$(ResumeGroupId)')";
                        return item;
                    }

                    return null;
                }
            );
        }

        private ItemGroupItem GenerateItem(string beforeProjectFile, string afterProjectFile, int buildGroup, IDependable studioProject) {
            var propertyList = new PropertyList();

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

                ItemGroupItem item = new ItemGroupItem(visualStudioProject.FullPath) {
                    [PropertiesKey] = propertyList.ToString(),
                    [BuildGroupId] = buildGroup.ToString(CultureInfo.InvariantCulture),
                    ["Configuration"] = visualStudioProject.BuildConfiguration.ConfigurationName,
                    ["Platform"] = visualStudioProject.BuildConfiguration.PlatformName,
                    ["AdditionalProperties"] = $"Configuration={visualStudioProject.BuildConfiguration.ConfigurationName}; Platform={visualStudioProject.BuildConfiguration.PlatformName}",
                    ["IsWebProject"] = visualStudioProject.IsWebProject.ToString(),
                    // Indicates this file is not part of the build system
                    ["IsProjectFile"] = bool.TrueString,
                };
                return item;
            }

            // TODO: Do we need this?
            //ExpertModule marker = studioProject as ExpertModule;
            //if (marker != null) {
            //    string solutionDirectoryPath = new DirectoryInfo(fileSystem.Root).Name == marker.Name ? fileSystem.Root : Path.Combine(fileSystem.Root, marker.Name);
            //    var properties = AddBuildProperties(propertyList, fileSystem, solutionDirectoryPath);

            //    ItemGroupItem item = new ItemGroupItem(beforeProjectFile) {
            //        [PropertiesKey] = properties.ToString(),
            //        [BuildGroupId] = buildGroup.ToString(CultureInfo.InvariantCulture)
            //    };

            //    return item;
            //}

            DirectoryNode node = studioProject as DirectoryNode;
            if (node != null) {
                string solutionDirectoryPath = node.Directory;
                var properties = AddBuildProperties(propertyList, fileSystem, solutionDirectoryPath);

                properties.Add("SolutionRoot=" + solutionDirectoryPath);

                ItemGroupItem item = new ItemGroupItem(node.IsPostTargets ? afterProjectFile : beforeProjectFile) {
                    [PropertiesKey] = properties.ToString(),
                    [BuildGroupId] = buildGroup.ToString(CultureInfo.InvariantCulture),

                    // Indicates this file is part of the build system itself
                    ["IsPostTargets"] = node.IsPostTargets ? bool.TrueString : bool.FalseString,
                    ["IsPreTargets"] = !node.IsPostTargets ? bool.TrueString : bool.FalseString,
                    ["IsProjectFile"] = bool.FalseString,
                };
                return item;
            }

            return null;
        }

        private PropertyList AddBuildProperties(PropertyList propertyList, IFileSystem2 fileSystem, string solutionDirectoryPath) {
            string responseFile = Path.Combine(solutionDirectoryPath, "Build", Path.ChangeExtension(Constants.EntryPointFile, "rsp"));
            if (fileSystem.FileExists(responseFile)) {
                using (var reader = new StreamReader(fileSystem.OpenFile(responseFile))) {
                    var propertiesText = reader.ReadToEnd();

                    var properties = propertiesText.Split(newLineArray, StringSplitOptions.None);

                    // We want to be able to specify the flavor globally in a build all so remove it from the property set
                    properties = RemoveFlavor(properties);

                    propertyList.Add(CreateSinglePropertyLine(properties));
                    propertyList.Add(PathUtility.EnsureTrailingSlash($"SolutionDirectoryPath={solutionDirectoryPath}"));
                }
            }

            return propertyList;
        }

        private PropertyList AddSolutionConfigurationProperties(ConfiguredProject visualStudioProject, PropertyList propertyList) {
            propertyList.Add($"SolutionRoot={visualStudioProject.SolutionRoot}");
            propertyList.Add($"Configuration={visualStudioProject.BuildConfiguration.ConfigurationName}");
            propertyList.Add($"Platform={visualStudioProject.BuildConfiguration.PlatformName}");

            AddMetaProjectProperties(visualStudioProject, propertyList);
            return propertyList;
        }

        private static void AddMetaProjectProperties(ConfiguredProject visualStudioProject, PropertyList propertiesList) {
            // MSBuild metaproj compatibility items (from the generated solution.sln.metaproj)
            // The following properties are 'macros' that are available via IDE for
            // pre and post build steps. However, they are not defined when directly building
            // a project from the command line, only when building a solution.
            // A lot of stuff doesn't work if they aren't present (web publishing targets for example) so we add them in as compatibility items 
            propertiesList.Add($"SolutionDir={visualStudioProject.SolutionRoot}");
            propertiesList.Add($"SolutionExt={Path.GetExtension(visualStudioProject.SolutionFile)}");
            propertiesList.Add($"SolutionFileName={Path.GetFileName(visualStudioProject.SolutionFile)}");
            propertiesList.Add($"SolutionPath={visualStudioProject.SolutionRoot}");
            propertiesList.Add($"SolutionName={Path.GetFileNameWithoutExtension(visualStudioProject.SolutionFile)}");
        }

        private string[] RemoveFlavor(string[] properties) {
            // TODO: Remove and use TreatAsLocalProperty
            IEnumerable<string> newProperties = properties.Where(p => p.IndexOf("BuildFlavor", StringComparison.InvariantCultureIgnoreCase) == -1);

            return newProperties.ToArray();
        }

        private string CreateSinglePropertyLine(string[] properties) {
            IList<string> lines = new List<string>();

            foreach (string property in properties) {
                if (property.StartsWith("/p:")) {
                    string line = property.Substring(property.IndexOf("/p:", StringComparison.Ordinal) + 3).Replace("\"", "").Trim();

                    if (!line.StartsWith("BuildInParallel")) {
                        lines.Add(line);
                    }
                }
            }

            return string.Join(";", lines);
        }
    }

}
