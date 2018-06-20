using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.MSBuild;

namespace Aderant.Build.DependencyAnalyzer {
    /// <summary>
    /// Represents a dynamic MSBuild project which will build a set of projects in dependency order and in parallel
    /// </summary>
    internal class DynamicProject {
        private readonly IFileSystem2 fileSystem;
        private const string InitializeTargets = @"Build\ModuleBuild.Initialize.targets";
        private const string CompletionTargets = @"Build\ModuleBuild.Completion.targets";

        public DynamicProject(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        public Project GenerateProject(List<List<IDependencyRef>> projectGroups, string buildFrom) {
            Project project = new Project();

            // Create a list of call targets for each build
            Target afterCompile = new Target("AfterCompile");

            bool buildFromHere = string.IsNullOrEmpty(buildFrom);

            // Since we may resequence things we want the to start the numbering at 0
            int buildGroupCount = 0;

            string sequenceFile = Path.Combine(fileSystem.Root, "BuildSequence.txt");
            using (StreamWriter outputFile = new StreamWriter(sequenceFile, false)) {
                for (int i = 0; i < projectGroups.Count; i++) {
                    List<IDependencyRef> projectGroup = projectGroups[i];

                    outputFile.WriteLine($"Group ({i})");
                    foreach (var dependencyRef in projectGroup) {
                        var isDirty = (dependencyRef as VisualStudioProject)?.IsDirty == true;
                        outputFile.WriteLine($"|   |---{dependencyRef.Name}" + (isDirty ? " *" : ""));
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

                    ItemGroup itemGroup = new ItemGroup("Build", CreateItemGroupMember(projectGroup, buildGroupCount, buildFrom));

                    // e.g. <Target Name="Build2">
                    Target build = new Target("Build" + buildGroupCount.ToString(CultureInfo.InvariantCulture));
                    if (buildGroupCount > 0) {
                        var target = project.Elements.OfType<Target>().FirstOrDefault(t => t.Name == $"Build{buildGroupCount-1}");
                        if (target != null) {
                            build.DependsOnTargets.Add(target);
                        }
                    }

                    project.Add(itemGroup);

                    build.Add(
                        new MSBuildTask {
                            BuildInParallel = true,
                            StopOnFirstFailure = true,
                            //Projects = $"@({itemGroup.Name})" // use @ so MSBuild will expand the list for us
                            Projects = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)), InitializeTargets),
                            Properties = $"DirectoryBuildFile=$(MSBuildThisFileFullPath);BuildGroup={buildGroupCount.ToString()};TotalNumberOfBuildGroups=$(TotalNumberOfBuildGroups)",
                        });

                    project.Add(build);

                    // e.g <Target Name="AfterCompile" DependsOnTargets="Build0;...n">;
                    afterCompile.DependsOnTargets.Add(new Target(build.Name));

                    buildGroupCount++;
                }
            }

            project.Add(new PropertyGroup(new Dictionary<string, string> { { "TotalNumberOfBuildGroups", buildGroupCount.ToString(CultureInfo.InvariantCulture) } }));
            project.Add(afterCompile);

            // The target that MSBuild will call into to start the build
            project.DefaultTarget = afterCompile;

            return project;
        }

        private IEnumerable<ItemGroupItem> CreateItemGroupMember(List<IDependencyRef> projectGroup, int buildGroup, string buildFrom) {
            return projectGroup.Select(
                studioProject => {
                    // there are two new ways to pass properties in item metadata, Properties and AdditionalProperties. 
                    // The difference can be confusing and very problematic if used incorrectly.
                    // The difference is that if you specify properties using the Properties metadata then any properties defined using the Properties attribute 
                    // on the MSBuild Task will be ignored. 
                    // In contrast to that if you use the AdditionalProperties metadata then both values will be used, with a preference going to the AdditionalProperties values.

                    VisualStudioProject visualStudioProject = studioProject as VisualStudioProject;

                    if (visualStudioProject != null) {
                        if ( !visualStudioProject.IncludeInBuild ) {
                            return null;
                        }

                        string properties = AddBuildProperties(visualStudioProject, fileSystem, visualStudioProject.SolutionRoot);

                        if (visualStudioProject.SolutionDirectoryName != buildFrom) {
                            properties += ";RunCodeAnalysisOnThisProject=false";
                        }

                        ItemGroupItem item = new ItemGroupItem(visualStudioProject.Path) {
                            ["AdditionalProperties"] = properties,
                            ["Configuration"] = visualStudioProject.BuildConfiguration.ConfigurationName,
                            ["Platform"] = visualStudioProject.BuildConfiguration.PlatformName,
                            ["BuildGroup"] = buildGroup.ToString(CultureInfo.InvariantCulture),
                            ["IsWebProject"] = visualStudioProject.IsWebProject.ToString()
                        };

                        return item;
                    }

                    ExpertModule marker = studioProject as ExpertModule;
                    if (marker != null) {
                    string solutionDirectoryPath = new DirectoryInfo(fileSystem.Root).Name == marker.Name ? fileSystem.Root : Path.Combine(fileSystem.Root, marker.Name);
                        string properties = AddBuildProperties(null, fileSystem, solutionDirectoryPath);

                        ItemGroupItem item = new ItemGroupItem(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)), InitializeTargets)) {
                            ["AdditionalProperties"] = properties,
                            ["BuildGroup"] = buildGroup.ToString(CultureInfo.InvariantCulture)
                        };
                        return item;
                    }

                    DirectoryNode node = studioProject as DirectoryNode;
                    if (node != null) {
                        if (node.IsCompletion) {
                            string solutionDirectoryPath = new DirectoryInfo(fileSystem.Root).Name == node.ModuleName ? fileSystem.Root : Path.Combine(fileSystem.Root, node.ModuleName);
                            string properties = AddBuildProperties(null, fileSystem, solutionDirectoryPath);

                            ItemGroupItem item = new ItemGroupItem(
                                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)), CompletionTargets)) {
                                ["AdditionalProperties"] = properties,
                                ["BuildGroup"] = buildGroup.ToString(CultureInfo.InvariantCulture)
                            };
                            return item;
                        }
                    }

                    return null;
                }
            );
        }

        private static readonly char[] newLineArray = Environment.NewLine.ToCharArray();

        private string AddBuildProperties(VisualStudioProject visualStudioProject, IFileSystem2 fileSystem, string solutionDirectoryPath) {
            string responseFile = Path.Combine(solutionDirectoryPath, "Build", Path.ChangeExtension(Constants.EntryPointFile, "rsp"));

            List<string> propertiesList = new List<string>();

            if (fileSystem.FileExists(responseFile)) {
                using (var reader = new StreamReader(fileSystem.OpenFile(responseFile))) {
                    var propertiesText = reader.ReadToEnd();

                    var properties = propertiesText.Split(newLineArray, StringSplitOptions.None);

                    // We want to be able to specify the flavor globally in a build all so remove it from the property set
                    properties = RemoveFlavor(properties);

                    propertiesList.Add(CreateSinglePropertyLine(properties));

                    propertiesList.Add($"SolutionDirectoryPath={solutionDirectoryPath}\\");

                    if (visualStudioProject != null) {
                        propertiesList.Add($"SolutionRoot={visualStudioProject.SolutionRoot}");
                        propertiesList.Add($"BuildProjectReferences={visualStudioProject.IsDirty.ToString()}");

                        AddMetaProjectProperties(visualStudioProject, propertiesList);

                        if (visualStudioProject.BuildConfiguration != null) {
                            propertiesList.Add($"Configuration={visualStudioProject.BuildConfiguration.ConfigurationName}");
                            propertiesList.Add($"Platform={visualStudioProject.BuildConfiguration.PlatformName}");
                        }
                    }}
            }

            return string.Join("; ", propertiesList);
        }

        private static void AddMetaProjectProperties(VisualStudioProject visualStudioProject, List<string> propertiesList) {
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