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
        [Parameter(Mandatory = false, HelpMessage = "Disables writing configuration files to modules.", Position = 0)]
        public SwitchParameter NoConfig { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Whether to force download and re-installation of all dependencies.", Position = 1)]
        public SwitchParameter Force { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Controls if the package resolver should be run. Use this switch if you have added or removed packages. (Prefer this over Force)", Position = 2)]
        public SwitchParameter Update { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Does not modify the paket.dependencies file in anyway. E.g does not add or remove sources.", Position = 3)]
        public SwitchParameter ReadOnly { get; set; }

        internal static readonly string EditorConfig = ".editorconfig";
        internal static readonly string MSTestV2CommonProject = "MSTest_V2_Common.proj";
        internal static readonly string ReSharperSettings = "sln.DotSettings";
        internal static readonly string AbsolutePathToken = "[ABSOLUTEPATH]";
        internal static readonly string RelativePathToken = "[RELATIVEPATH]";

        private static readonly string[] reservedDirectories = new string[] {
            "Build"
        };

        private readonly IFileSystem2 fileSystem;

        public GetDependencies() {
            fileSystem = new PhysicalFileSystem(string.Empty, this.Logger);
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
            workflow.ReadOnly = ReadOnly.ToBool();

            bool update = !string.Equals(currentDirectory, root, StringComparison.OrdinalIgnoreCase);
            if (!update) {
                if (Update.IsPresent) {
                    update = Update.ToBool();
                }
            }

            Logger.Debug($"Update: {update}");

            workflow.Run(update, MyInvocation.BoundParameters.ContainsKey("Verbose") || MyInvocation.UnboundArguments.Contains("Verbose"), base.CancellationToken);

            if (!NoConfig.IsPresent) {
                WriteConfigFiles(workflow.DirectoriesInBuild);
            }
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

            string reSharperSettingsFile = Path.Combine(profileDirectory, ReSharperSettings);
            string content = fileSystem.ReadAllText(reSharperSettingsFile);

            List<Action<string>> actions = new List<Action<string>> {
                directory => LinkFile(profileDirectory, EditorConfig, directory),
                directory => LinkFile(buildScriptsDirectory, MSTestV2CommonProject, directory, "Test"),
                directory => CopyReSharperSettings(content, directory, buildScriptsDirectory)
            };

            Parallel.ForEach(modulesInBuild, directory => actions.ForEach(action => action.Invoke(directory)));
        }

        private bool IsReservedDirectory(string path) {
            return reservedDirectories.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        }

        internal void LinkFile(string sourceDirectory, string fileName, string destination, string target = null) {
            if (IsReservedDirectory(destination)) {
                return;
            }

            if (target != null) {
                destination = Path.Combine(destination, target);
            }

            string destinationLink = Path.Combine(destination, fileName);

            if (fileSystem.DirectoryExists(destination)) {
                Logger.Info("Creating link to: {0} file at: '{1}'.", fileName, destinationLink);
                fileSystem.CreateFileHardLink(destinationLink, Path.Combine(sourceDirectory, fileName));
            }
        }

        private IList<string> GetSolutionFilesInDirectory(string path) {
            return !fileSystem.DirectoryExists(path) ? new List<string>(0) : fileSystem.GetFiles(path, "*.sln", false).ToList();
        }

        internal static string GetReSharperSettingsFileName(string solution) {
            return string.Concat(Path.GetFileNameWithoutExtension(solution), ".", ReSharperSettings);
        }

        internal void CopyReSharperSettings(string content, string destination, string buildScriptsDirectory) {
            if (IsReservedDirectory(destination)) {
                return;
            }

            var solutions = GetSolutionFilesInDirectory(destination);

            if (!solutions.Any()) {
                return;
            }

            // Update the [ABSOLUTEPATH] and [RELATIVEPATH] tokens.
            string repositoryDirectory = Path.GetFullPath(Path.Combine(buildScriptsDirectory, $"..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}.."));
            content = content.Replace(AbsolutePathToken, repositoryDirectory);
            content = content.Replace(RelativePathToken, PathUtility.MakeRelative(destination, repositoryDirectory));

            foreach (string solution in solutions) {
                string fileName = GetReSharperSettingsFileName(solution);

                string destinationFile = Path.Combine(destination, fileName);
                Logger.Info("Writing ReSharper settings file to: '{0}'.", destinationFile);

                fileSystem.WriteAllText(destinationFile, content);
            }
        }
    }
}
