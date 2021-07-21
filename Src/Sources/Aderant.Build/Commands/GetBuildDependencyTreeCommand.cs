using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
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

        [Parameter(Mandatory = false, Position = 3, HelpMessage = "Specifies if the tree should consider the current cache state to produce a graph that can skip projects.")]
        public SwitchParameter UseBuildCache { get; set; }

        [Parameter(Mandatory = false, Position = 4, HelpMessage = "Specifies if the tree should consider external packages during the graph construction.")]
        public SwitchParameter SkipNugetPackageHashCheck { get; set; }

        [Parameter(Mandatory = false, Position = 5, HelpMessage = "Specifies the location of the build cache.")]
        public string DropLocation { get; set; } = @"\\aderant.com\expert-ci\prebuilts\v1";


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

            IFileSystem fileSystem = new PhysicalFileSystem();

            var fs = new ProjectSequencer(logger, fileSystem);

            var context = CreateBuildOperationContext();
            context.Switches = new BuildSwitches {
                SkipNugetPackageHashCheck = SkipNugetPackageHashCheck.ToBool()
            };

            var buildPlan = fs.CreatePlan(context, new OrchestrationFiles(), projectDependencyGraph, true);
            var groups = projectDependencyGraph.GetBuildGroups();

            string treeText = ProjectSequencer.PrintBuildTree(groups, ShowPath.IsPresent);

            Host.UI.Write(treeText);

            if (WriteFile.IsPresent) {
                ProjectSequencer.WriteBuildTree(fileSystem, this.SessionState.Path.CurrentFileSystemLocation.Path, treeText);
                Host.UI.Write($@"{Environment.NewLine}Wrote build tree to: '{this.SessionState.Path.CurrentFileSystemLocation.Path}\BuildTree.txt'.");
            }
        }

        private BuildOperationContext CreateBuildOperationContext() {
            var context = new BuildOperationContext();

            if (UseBuildCache.ToBool()) {
                var sourceTreeMetadata = GetSourceTreeMetadata();
                var buildMetadata = GetBuildStateMetadata(sourceTreeMetadata);

                context.SourceTreeMetadata = sourceTreeMetadata;
                context.BuildStateMetadata = buildMetadata;
            } else {
                context.SourceTreeMetadata = new SourceTreeMetadata();
                context.BuildStateMetadata = new BuildStateMetadata();
            }

            return context;
        }

        private SourceTreeMetadata GetSourceTreeMetadata() {
            var command = new Command("Get-SourceTreeMetadata");
            command.Parameters.Add("SourceDirectory", Directories[0]);
            command.Parameters.Add(nameof(GetSourceTreeMetadataCommand.IncludeLocalChanges), true);
            var sourceTreeMetadata = InvokeInternal(command) as SourceTreeMetadata;
            return sourceTreeMetadata;
        }

        private BuildStateMetadata GetBuildStateMetadata(SourceTreeMetadata sourceTreeMetadata) {
            var command = new Command("Get-BuildStateMetadata");
            command.Parameters.Add(nameof(GetBuildStateMetadataCommand.BucketIds), sourceTreeMetadata.BucketIds.Select(s => s.Id).ToArray());
            command.Parameters.Add(nameof(GetBuildStateMetadataCommand.DropLocation), DropLocation);

            var buildMetadata = InvokeInternal(command) as BuildStateMetadata;
            return buildMetadata;
        }

        private static object InvokeInternal(Command command) {
            Pipeline pipeline = Runspace.DefaultRunspace.CreateNestedPipeline();
            pipeline.Commands.Add(command);
            var result = pipeline.Invoke();
            return result[0].BaseObject;
        }

        protected override void StopProcessing() {
            cts.Cancel();
            base.StopProcessing();
        }
    }
}
