using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.Commands {

    [Cmdlet(VerbsCommon.Get, "BuildDependencyTree")]
    public class GetBuildDependencyTreeCommand : PSCmdlet {
        private CancellationTokenSource cts;

        [Parameter(Mandatory = false, Position = 0)]
        public string[] Directories { get; set; }

        [Parameter(Mandatory = false, Position = 1, HelpMessage = "Specifies if the full project path should be printed.")]
        public SwitchParameter ShowPath { get; set; }

        [Parameter(Mandatory = false, Position = 2, HelpMessage = "Specifies if the build tree should be written to file.")]
        public SwitchParameter WriteFile { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            List<string> resolvedPaths = new List<string>();

            if (Directories == null || Directories.Length == 0) {
                Directories = new[] { this.SessionState.Path.CurrentFileSystemLocation.Path };
            }

            foreach (var path in Directories) {
                resolvedPaths.AddRange(this.SessionState.Path.GetResolvedPSPathFromPSPath(path).Select(s => s.Path));
            }

            cts = new CancellationTokenSource();

            var logger = new PowerShellLogger(this);

            var projectTree = ProjectTree.CreateDefaultImplementation(logger);
            var collector = new BuildDependenciesCollector();
            collector.ProjectConfiguration = ConfigurationToBuild.Default;

            if (collector.ExtensibilityImposition == null) {
                collector.ExtensibilityImposition = new ExtensibilityImposition(null);
            }

            collector.ExtensibilityImposition.RequireSynchronizedOutputPaths = true;

            projectTree.LoadProjects(
                resolvedPaths,
                new[] { @"\__" },
                cts.Token);

            projectTree.CollectBuildDependencies(collector, cts.Token).Wait(cts.Token);

            var buildDependencyGraph = projectTree.CreateBuildDependencyGraph(collector);

            var projectDependencyGraph = new ProjectDependencyGraph(buildDependencyGraph);

            var fs = new ProjectSequencer(logger, new PhysicalFileSystem());
            fs.Sequence(new BuildSwitches(), false, null, projectDependencyGraph, new BuildMetadata());

            var groups = projectDependencyGraph.GetBuildGroups();

            string treeText = ProjectSequencer.PrintBuildTree(groups, ShowPath.IsPresent);

            Host.UI.Write(treeText);

            if (WriteFile.IsPresent) {
                ProjectSequencer.WriteBuildTree(new PhysicalFileSystem(), this.SessionState.Path.CurrentFileSystemLocation.Path, treeText);
                Host.UI.Write($@"{Environment.NewLine}Wrote build tree to: '{this.SessionState.Path.CurrentFileSystemLocation.Path}\BuildTree.txt'.");
            }
        }

        protected override void StopProcessing() {
            cts.Cancel();
            base.StopProcessing();
        }
    }
}
