using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
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

            projectTree.LoadProjects(Directories, true, null);
            projectTree.CollectBuildDependencies(collector).Wait();
            var buildDependencyGraph = projectTree.CreateBuildDependencyGraph(collector);

            var groups = buildDependencyGraph.GetBuildGroups(buildDependencyGraph.GetDependencyOrder());

            var topLevelNodes = new List<TreePrinter.Node>();

            int i = 0;
            foreach (var group in groups) {
                var topLevelNode = new TreePrinter.Node();
                topLevelNode.Name = "Group " +  i;
                topLevelNode.Children = group.Select(s => new TreePrinter.Node { Name = s.Id }).ToList();
                i++;

                topLevelNodes.Add(topLevelNode);
            }

            TreePrinter.Print(topLevelNodes, Host.UI.Write);
        }
    }
}
