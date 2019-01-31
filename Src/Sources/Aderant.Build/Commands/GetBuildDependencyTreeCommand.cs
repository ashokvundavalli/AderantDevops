using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;
using Aderant.Build.Utilities;

namespace Aderant.Build.Commands {

    [Cmdlet("Get", "BuildDependencyTree")]
    public class GetBuildDependencyTreeCommand : PSCmdlet {

        [Parameter(Mandatory = false, Position = 0)]
        public string[] Directories { get; set; }

        [Parameter(Mandatory = false, Position = 1, HelpMessage = "Specifies if the full project path should be printed.")]
        public bool ShowPath { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            if (Directories == null || Directories.Length == 0) {
                Directories = new[] { SessionState.Path.CurrentFileSystemLocation.Path };
            }

            var logger = new PowerShellLogger(this.Host);

            var projectTree = ProjectTree.CreateDefaultImplementation(logger);
            var collector = new BuildDependenciesCollector();
            collector.ProjectConfiguration = ConfigurationToBuild.Default;

            projectTree.LoadProjects(
                Directories,
                new[] { @"\__" });

            projectTree.CollectBuildDependencies(collector).Wait();

            var buildDependencyGraph = projectTree.CreateBuildDependencyGraph(collector);

            var projectDependencyGraph = new ProjectDependencyGraph(buildDependencyGraph);

            var fs = new ProjectSequencer(logger, new PhysicalFileSystem());
            fs.Sequence(false, false, null, projectDependencyGraph);

            var groups = projectDependencyGraph.GetBuildGroups(projectDependencyGraph.GetDependencyOrder());

            string treeText = ProjectSequencer.PrintBuildTree(groups, ShowPath);

            Host.UI.Write(treeText);
        }
    }
}