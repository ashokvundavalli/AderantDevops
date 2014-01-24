using System.Collections.Generic;
using System.Linq;
using DependencyAnalyzer.MSBuild;
using DependencyAnalyzer.Providers;

namespace DependencyAnalyzer.Process {
    /// <summary>
    /// Represents a dynamic MSBuild project which will build a set of Expert modules in dependency order and in parallel
    /// </summary>
    internal class DynamicProject {
        private readonly List<Build> builds;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicProject"/> class.
        /// </summary>
        /// <param name="builds">The builds.</param>
        public DynamicProject(IEnumerable<Build> builds) {
            this.builds = builds.ToList();
        }

        /// <summary>
        /// Generates the tree as a MSBuild document.
        /// </summary>
        /// <returns></returns>
        public Project GenerateProject(string modulesDirectory) {
            Project project = new Project();

            // Create a list of call targets for each build
            Target afterCompile = new Target("AfterCompile");

            for (int i = 0; i < builds.Count; i++) {
                IEnumerable<string> modulesInGroup = builds[i].Modules.Select(m => m.Name);

                // If there are no modules in the item group, no point generating any Xml for this build node
                if (!modulesInGroup.Any()) {
                    continue;
                }

                ItemGroup itemGroup = new ItemGroup("Build" + i, modulesInGroup.Select(m => PathHelper.Aggregate(modulesDirectory, m, "Build", "TFSBuild.proj")));
                project.Add(itemGroup);

                // e.g. <Target Name="Build2">
                Target build = new Target("Build" + i);
                build.Add(new MSBuildTask {
                    BuildInParallel = true,
                    Projects = string.Format("@({0})", itemGroup.Name) // use @ so MSBuild will expand the list for us
                });
                
                project.Add(build);

                // e.g <Target Name="AfterCompile" DependsOnTargets="Build0;...n">;
                afterCompile.DependsOnTargets.Add(new Target(build.Name));
            }

            project.Add(afterCompile);

            // The target that MSBuild will call into to start the build
            project.DefaultTarget = afterCompile;

            return project;
        }
    }
}