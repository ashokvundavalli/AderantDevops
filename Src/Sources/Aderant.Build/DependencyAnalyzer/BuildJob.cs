using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.MSBuild;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Represents a dynamic MSBuild project which will build a set of projects in dependency order and in parallel
    /// </summary>
    internal class BuildJob {

        private static readonly char[] newLineArray = Environment.NewLine.ToCharArray();
        private readonly IFileSystem2 fileSystem;

        public BuildJob(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        public Project GenerateProject(List<List<IDependencyRef>> projectGroups, BuildJobFiles instance, string buildFrom) {
            Project project = new Project();

            // Create a list of call targets for each build
            Target afterCompile = new Target("AfterCompile");

            bool buildFromHere = string.IsNullOrEmpty(buildFrom);

            int buildGroupCount = 0;

            ItemGroup itemGroup = new ItemGroup("AllProjectsToBuild");
            
            //string sequenceFile = Path.Combine(fileSystem.Root, "BuildSequence.txt");
     //       using (StreamWriter outputFile = new StreamWriter(sequenceFile, false)) {
                for (int i = 0; i < projectGroups.Count; i++) {
                    List<IDependencyRef> projectGroup = projectGroups[i];

          //          outputFile.WriteLine($"Group ({i})");
                    foreach (var dependencyRef in projectGroup) {
                        var isDirty = (dependencyRef as VisualStudioProject)?.IsDirty == true;
          //              outputFile.WriteLine($"|   |---{dependencyRef.Name}" + (isDirty ? " *" : ""));
                    }

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
                    
                    itemGroup = new ItemGroup(itemGroup.Name, itemGroup.Include.Concat(CreateItemGroupMember(instance.BeforeProjectFile, instance.AfterProjectFile, projectGroup, buildGroupCount, buildFrom)));

                    // e.g. <Target Name="Foo">
                    Target build = new Target("Run" + CreateGroupName(buildGroupCount));
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
                            Projects = instance.JobRunFile,
                            Properties = $"InstanceProjectFile=$(MSBuildThisFileFullPath);BuildGroup={buildGroupCount.ToString()};TotalNumberOfBuildGroups=$(TotalNumberOfBuildGroups);BuildInParallel=$(BuildInParallel)",
                        });
                  
                    project.Add(build);

                    // e.g <Target Name="AfterCompile" DependsOnTargets="Build0;...n">;
                    afterCompile.DependsOnTargets.Add(new Target(build.Name));

                    buildGroupCount++;
                }
     //       }

            project.Add(new PropertyGroup(new Dictionary<string, string> { { "TotalNumberOfBuildGroups", buildGroupCount.ToString(CultureInfo.InvariantCulture) } }));
            project.Add(itemGroup);
            project.Add(afterCompile);

            // The target that MSBuild will call into to start the build
            project.DefaultTarget = afterCompile;

            return project;
        }

        private static string CreateGroupName(int buildGroupCount) {
            return "ProjectsToBuild" + buildGroupCount.ToString(CultureInfo.InvariantCulture);
        }

        private IEnumerable<ItemGroupItem> CreateItemGroupMember(string beforeProjectFile, string afterProjectFile, List<IDependencyRef> projectGroup, int buildGroup, string buildFrom) {
            return projectGroup.Select(
                studioProject => {
                    var propertyList = new PropertyList();

                    // there are two new ways to pass properties in item metadata, Properties and AdditionalProperties. 
                    // The difference can be confusing and very problematic if used incorrectly.
                    // The difference is that if you specify properties using the Properties metadata then any properties defined using the Properties attribute 
                    // on the MSBuild Task will be ignored. 
                    // In contrast to that if you use the AdditionalProperties metadata then both values will be used, with a preference going to the AdditionalProperties values.

                    VisualStudioProject visualStudioProject = studioProject as VisualStudioProject;

                    if (visualStudioProject != null) {
                        if (!visualStudioProject.IncludeInBuild) {
                            return null;
                        }

                        propertyList = AddSolutionConfigurationProperties(visualStudioProject, propertyList);

                        if (visualStudioProject.SolutionDirectoryName != buildFrom) {
                            propertyList.Add("RunCodeAnalysisOnThisProject=false");
                        }

                        ItemGroupItem item = new ItemGroupItem(visualStudioProject.Path) {
                            ["AdditionalProperties"] = propertyList.ToString(),
                            ["Configuration"] = visualStudioProject.ProjectBuildConfiguration.ConfigurationName,
                            ["Platform"] = visualStudioProject.ProjectBuildConfiguration.PlatformName,
                            ["BuildGroup"] = buildGroup.ToString(CultureInfo.InvariantCulture),
                            ["IsWebProject"] = visualStudioProject.IsWebProject.ToString()
                        };

                        return item;
                    }

                    ExpertModule marker = studioProject as ExpertModule;
                    if (marker != null) {
                        string solutionDirectoryPath = new DirectoryInfo(fileSystem.Root).Name == marker.Name ? fileSystem.Root : Path.Combine(fileSystem.Root, marker.Name);
                        var properties = AddBuildProperties(propertyList, fileSystem, solutionDirectoryPath);

                        ItemGroupItem item = new ItemGroupItem(beforeProjectFile) {
                            ["AdditionalProperties"] = properties.ToString(),
                            ["BuildGroup"] = buildGroup.ToString(CultureInfo.InvariantCulture)
                        };
                        return item;
                    }

                    DirectoryNode node = studioProject as DirectoryNode;
                    if (node != null) {
                        string solutionDirectoryPath = new DirectoryInfo(fileSystem.Root).Name == node.ModuleName ? fileSystem.Root : Path.Combine(fileSystem.Root, node.ModuleName);
                        var properties = AddBuildProperties(propertyList, fileSystem, solutionDirectoryPath);
                        properties.Add("SolutionRoot=" + solutionDirectoryPath);

                        ItemGroupItem item = new ItemGroupItem(node.IsCompletion ? afterProjectFile : beforeProjectFile) {
                            ["AdditionalProperties"] = properties.ToString(),
                            ["BuildGroup"] = buildGroup.ToString(CultureInfo.InvariantCulture)
                        };
                        return item;

                    }

                    return null;
                }
            );
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

        private static PropertyList AddSolutionConfigurationProperties(VisualStudioProject visualStudioProject, PropertyList propertyList) {
                propertyList.Add($"SolutionRoot={visualStudioProject.SolutionRoot}");
                propertyList.Add($"BuildProjectReferences={visualStudioProject.IsDirty.ToString()}");

                AddMetaProjectProperties(visualStudioProject, propertyList);

                if (visualStudioProject.ProjectBuildConfiguration != null) {
                    propertyList.Add($"Configuration={visualStudioProject.ProjectBuildConfiguration.ConfigurationName}");
                    propertyList.Add($"Platform={visualStudioProject.ProjectBuildConfiguration.PlatformName}");
                }
            

            return propertyList;
        }

        private static void AddMetaProjectProperties(VisualStudioProject visualStudioProject, PropertyList propertiesList) {
            // MSBuild metaproj compatibility items (from the generated solution.sln.metaproj)
            propertiesList.Add($"SolutionDir={visualStudioProject.SolutionRoot}");
            propertiesList.Add($"SolutionExt={Path.GetExtension(visualStudioProject.SolutionFile)}");
            propertiesList.Add($"SolutionFileName={Path.GetFileName(visualStudioProject.SolutionFile)}");
            propertiesList.Add($"SolutionPath={visualStudioProject.SolutionRoot}");
            propertiesList.Add($"SolutionName={Path.GetFileNameWithoutExtension(visualStudioProject.SolutionFile)}");
        }

        private string[] RemoveFlavor(string[] properties) {
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
