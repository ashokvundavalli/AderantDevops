using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using Aderant.Build.DependencyResolver;
using LibGit2Sharp;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.Get, "Dependencies"), Alias("gd")]
    public sealed class GetDependencies : BuildCmdlet {

        [Parameter(Mandatory = false, HelpMessage = "Whether or not to use Symbolic Links", Position = 0), ValidateNotNull]
        public SwitchParameter NoSymLinks { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Whether to force retrieval of dependencies", Position = 1), ValidateNotNull]
        public SwitchParameter Force { get; set; }

        internal static readonly string EditorConfig = ".editorconfig";
        internal static readonly string ResharperSettings = "sln.DotSettings";
        internal static readonly string AbsolutePathToken = "[ABSOLUTEPATH]";
        internal static readonly string RelativePathToken = "[RELATIVEPATH]";

        private static readonly string[] reservedDirectories = new string[] {
            "Build"
        };

        private readonly IFileSystem2 fileSystem;

        public GetDependencies() {
            fileSystem = new PhysicalFileSystem("", this.Logger);
        }

        internal GetDependencies(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        protected override void Process() {
            string currentDirectory = SessionState.Path.CurrentFileSystemLocation.Path;

            string repository = Repository.Discover(currentDirectory);

            if (string.IsNullOrWhiteSpace(repository)) {
                ThrowTerminatingError(new ErrorRecord(new LibGit2SharpException("Please use the Get-Dependencies command in a git repository."), string.Empty, ErrorCategory.InvalidArgument, nameof(repository)));
            }

            string root = Directory.GetParent(repository)?.Parent?.FullName;

            if (string.IsNullOrWhiteSpace(root)) {
                ThrowTerminatingError(new ErrorRecord(new LibGit2SharpException($"Unable to determine root for repository: '{repository}'."), string.Empty, ErrorCategory.InvalidArgument, nameof(root)));
            }

            (string branchConfigFile, string productManifestFile) = RunGetBuildConfigFilePaths(currentDirectory, root);

            var workflow = new ResolverWorkflow(Logger, fileSystem)
                .WithRootPath(root)
                .WithProductManifest(productManifestFile)
                .WithConfigurationFile(branchConfigFile)
                .WithDirectoriesInBuild(currentDirectory, root);

            workflow.Force = Force.ToBool();

            bool update = !string.Equals(currentDirectory, root, StringComparison.OrdinalIgnoreCase);

            Logger.Debug($"Update: {update}");

            workflow.Run(update, MyInvocation.BoundParameters.ContainsKey("Verbose") || MyInvocation.UnboundArguments.Contains("Verbose"), base.CancellationToken);

            WriteConfigFiles(workflow.DirectoriesInBuild);
        }

        private static (string branchConfigFile, string productManifestFile) RunGetBuildConfigFilePaths(string startingDirectory, string ceilingDirectory) {
            using (var pipeline = Runspace.DefaultRunspace.CreateNestedPipeline()) {
                var command = new Command("Get-BuildConfigFilePaths");
                command.Parameters.Add("startingDirectory", startingDirectory);
                command.Parameters.Add("ceilingDirectories", new[] {ceilingDirectory});

                pipeline.Commands.Add(command);
                var psObject = pipeline.Invoke().FirstOrDefault();

                var configInfo = psObject as dynamic;

                if (configInfo != null) {
                    return new ValueTuple<string, string>(configInfo.BranchConfigFile, configInfo.ProductManifestFile);
                }
            }

            return (null, null);
        }

        private void WriteConfigFiles(IEnumerable<string> modulesInBuild) {
            string buildScriptsDirectory = AppDomain.CurrentDomain.GetData("BuildScriptsDirectory").ToString();

            if (string.IsNullOrWhiteSpace(buildScriptsDirectory)) {
                ThrowTerminatingError(new ErrorRecord(new ArgumentException("Unable to retrieve value for BuildScriptsDirectory."), string.Empty, ErrorCategory.InvalidArgument, nameof(buildScriptsDirectory)));
            }

            string profileDirectory = Path.Combine(buildScriptsDirectory, "..\\Profile");

            string configFile = Path.Combine(profileDirectory, EditorConfig);
            string reSharperSettingsFile = Path.Combine(profileDirectory, ResharperSettings);
            string content = fileSystem.ReadAllText(reSharperSettingsFile);

            List<Action<string>> actions = new List<Action<string>> {
                directory => LinkEditorConfigFile(configFile, directory),
                directory => CopyReSharperSettings(content, directory, buildScriptsDirectory)
            };

            Parallel.ForEach(modulesInBuild, directory => actions.ForEach(action => action.Invoke(directory)));
        }

        private bool IsReservedDirectory(string path) {
            return reservedDirectories.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        }

        internal void LinkEditorConfigFile(string configFile, string directory) {
            if (IsReservedDirectory(directory)) {
                return;
            }

            string destinationLink = Path.Combine(directory, EditorConfig);

            fileSystem.CreateFileHardLink(destinationLink, configFile);
        }

        internal void CopyReSharperSettings(string content, string destination, string buildScriptsDirectory) {
            if (IsReservedDirectory(destination)) {
                return;
            }

            // Update the [ABSOLUTEPATH] and [RELATIVEPATH] tokens.
            string repositoryDirectory = Path.GetFullPath(Path.Combine(buildScriptsDirectory, @"..\..\.."));
            content = content.Replace(AbsolutePathToken, repositoryDirectory);
            content = content.Replace(RelativePathToken, PathUtility.MakeRelative(destination, repositoryDirectory));

            string destinationFile = Path.Combine(destination, ResharperSettings);
            Logger.Info("Writing ReSharper settings file to: '{0}'.", destinationFile);

            fileSystem.WriteAllText(destinationFile, content);
        }
    }
}
