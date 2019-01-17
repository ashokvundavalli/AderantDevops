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

        protected override void ProcessRecord() {
            base.ProcessRecord();

            if (Directories == null || Directories.Length == 0) {
                Directories = new[] { SessionState.Path.CurrentFileSystemLocation.Path };
            }

            var projectTree = ProjectTree.CreateDefaultImplementation(new PowerShellLogger(this.Host));
            var collector = new BuildDependenciesCollector();
            collector.ProjectConfiguration = ConfigurationToBuild.Default;

            projectTree.LoadProjects(
                Directories,
                new[] { @"\__" });

            projectTree.CollectBuildDependencies(collector).Wait();
            var buildDependencyGraph = projectTree.CreateBuildDependencyGraph(collector);

            var groups = buildDependencyGraph.GetBuildGroups(buildDependencyGraph.GetDependencyOrder());

            string treeText = ProjectSequencer.PrintBuildTree(groups);

            Host.UI.Write(treeText);
        }
    }
}