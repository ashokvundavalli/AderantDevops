using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Aderant.Build.Logging;
using LibGit2Sharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ILogger = Aderant.Build.Logging.ILogger;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.Get, "Dependencies"), Alias("gd")]
    public sealed class GetDependencies : PSCmdlet {

        [Parameter(Mandatory = false, HelpMessage = "Whether or not to use Symbolic Links", Position = 0), ValidateNotNull]
        public SwitchParameter NoSymLinks { get; set; }

        [Parameter(Mandatory = false, HelpMessage = "Whether to force retrieval of dependencies", Position = 1), ValidateNotNull]
        public SwitchParameter Force { get; set; }

        private static readonly string DependenciesFile = Constants.PaketDependencies;
        internal static readonly string EditorConfig = ".editorconfig";
        internal static readonly string ResharperSettings = "sln.DotSettings";
        internal static readonly string AbsolutePathToken = "[ABSOLUTEPATH]";
        internal static readonly string RelativePathToken = "[RELATIVEPATH]";

        private static readonly string[] ReservedDirectories = new string[] {
            "Build"
        };

        private readonly ILogger logger;
        private readonly IFileSystem2 fileSystem;

        public GetDependencies() {
            logger = new PowerShellLogger(this);
            fileSystem = new PhysicalFileSystem();
        }

        internal GetDependencies(ILogger logger, IFileSystem2 fileSystem) {
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            string currentDirectory = SessionState.Path.CurrentFileSystemLocation.Path;
            string repository = Repository.Discover(currentDirectory);

            if (string.IsNullOrWhiteSpace(repository)) {
                ThrowTerminatingError(new ErrorRecord(new LibGit2SharpException("Please use the Get-Dependencies command in a git repository."), string.Empty, ErrorCategory.InvalidArgument, nameof(repository)));
            }

            string root = Directory.GetParent(repository)?.Parent?.FullName;

            if (string.IsNullOrWhiteSpace(root)) {
                ThrowTerminatingError(new ErrorRecord(new LibGit2SharpException($"Unable to determine root for repository: '{repository}'."), string.Empty, ErrorCategory.InvalidArgument, nameof(root)));
            }

            string productManifest = Path.Combine(root, @"Build\ExpertManifest.xml");
            string branchConfig = Path.Combine(root, @"Build\BranchConfig.xml");

            Tasks.GetDependencies getDependenciesTask = new Tasks.GetDependencies {
                ProductManifest = fileSystem.FileExists(productManifest) ? productManifest : null,
                BranchConfigFile = fileSystem.FileExists(branchConfig) ? branchConfig : null,
                ModulesRootPath = root,
                EnableVerboseLogging = MyInvocation.BoundParameters.ContainsKey("Verbose") || MyInvocation.UnboundArguments.Contains("Verbose")
            };

            string[] modulesInBuild = GetModulesInBuild(root, currentDirectory, getDependenciesTask);

            getDependenciesTask.ConfigureTask();

            if (getDependenciesTask.Update) {
                CleanGeneratedPaketDependenciesFile(getDependenciesTask, root);
            }

            if (NoSymLinks.IsPresent) {
                getDependenciesTask.EnableReplication = false;
            }

            getDependenciesTask.ExecuteInternal(logger);

            string dependenciesDirectory = getDependenciesTask.DependenciesDirectory;
            if (!NoSymLinks.IsPresent && !string.IsNullOrWhiteSpace(dependenciesDirectory)) {
                CreateSymlinks(root, dependenciesDirectory, modulesInBuild, getDependenciesTask.EnableReplication);
            }

            WriteConfigFiles(modulesInBuild);
        }

        private void WriteConfigFiles(string[] modulesInBuild) {
            string buildScriptsDirectory = AppDomain.CurrentDomain.GetData("BuildScriptsDirectory").ToString();

            if (string.IsNullOrWhiteSpace(buildScriptsDirectory)) {
                ThrowTerminatingError(new ErrorRecord(new ArgumentException("Unable to retrieve value for BuildScriptsDirectory."), string.Empty, ErrorCategory.InvalidArgument, nameof(buildScriptsDirectory)));
            }

            string configFile = Path.GetFullPath(Path.Combine(buildScriptsDirectory, $@"..\Profile\{EditorConfig}"));
            string reSharperSettingsFile = Path.GetFullPath(Path.Combine(buildScriptsDirectory, $@"..\Profile\{ResharperSettings}"));
            string content = fileSystem.ReadAllText(reSharperSettingsFile);

            List<Action<string>> actions = new List<Action<string>> {
                directory => LinkEditorConfigFile(configFile, directory),
                directory => CopyReSharperSettings(content, directory, buildScriptsDirectory)
            };

            Parallel.ForEach(modulesInBuild, directory => actions.ForEach(action => action.Invoke(directory)));
        }

        private bool IsReservedDirectory(string path) {
            return ReservedDirectories.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
        }

        internal void LinkEditorConfigFile(string configFile, string directory) {
            if (IsReservedDirectory(directory)) {
                return;
            }

            string destinationLink = Path.Combine(directory, EditorConfig);

            if (!string.Equals(Path.GetPathRoot(configFile), Path.GetPathRoot(destinationLink))) {
                // Hard links cannot span drives.
                logger.Info($"Creating symbolic link for file: '{EditorConfig}' to: '{destinationLink}'.");
                fileSystem.CopyViaLink(configFile, destinationLink, PhysicalFileSystem.createSymlinkLink);
            } else {
                logger.Info($"Creating hard link file: '{EditorConfig}' to: '{destinationLink}'.");
                fileSystem.CopyViaLink(configFile, destinationLink, PhysicalFileSystem.createHardlink);
            }
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
            logger.Info($"Writing ReSharper settings file to: '{destinationFile}'.");

            fileSystem.WriteAllText(destinationFile, content);
        }

        private void CleanGeneratedPaketDependenciesFile(Tasks.GetDependencies getDependenciesTask, string root) {
            string dependenciesDirectory = getDependenciesTask.DependenciesDirectory;

            if (!string.IsNullOrWhiteSpace(dependenciesDirectory)) {
                // Remove the generated paket.dependencies file if it exists.
                string paketDependencies = Path.Combine(root, dependenciesDirectory, DependenciesFile);

                if (fileSystem.FileExists(paketDependencies)) {
                    fileSystem.DeleteFile(paketDependencies);
                }
            }
        }

        private string[] GetModulesInBuild(string root, string currentDirectory, Tasks.GetDependencies getDependenciesTask) {
            HashSet<string> modulesInBuild = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.Equals(root, currentDirectory, StringComparison.OrdinalIgnoreCase)) {
                if (fileSystem.FileExists(Path.Combine(root, DependenciesFile))) {
                    modulesInBuild.Add(root);
                } else {
                    string[] directories = Directory.GetDirectories(root);

                    foreach (string directory in directories) {
                        if (Directory.GetFiles(directory, DependenciesFile, SearchOption.TopDirectoryOnly).Length == 1) {
                            modulesInBuild.Add(directory);
                        }
                    }
                }

                getDependenciesTask.Update = Force.IsPresent;
            } else {
                modulesInBuild.Add(fileSystem.FileExists(Path.Combine(currentDirectory, DependenciesFile)) ? currentDirectory : root);
                getDependenciesTask.Update = Force.IsPresent || string.IsNullOrWhiteSpace(getDependenciesTask.DependenciesDirectory);
            }

            if (modulesInBuild.Count == 0) {
                ThrowTerminatingError(new ErrorRecord(
                    new FileNotFoundException(
                        $"Unable to locate any paket.dependencies files in any modules in root: {root}."), string.Empty,
                    ErrorCategory.InvalidOperation, string.Empty));
            }

            getDependenciesTask.ModulesInBuild = modulesInBuild.Select(x => (ITaskItem) new TaskItem(x)).ToArray();

            return modulesInBuild.ToArray();
        }

        private void CreateSymlinks(string root, string dependenciesDirectory, string[] directories, bool replicationEnabled) {
            Tasks.MakeSymlink makeSymlinkTask = new Tasks.MakeSymlink {
                Type = "D",
            };

            string dependenciesTarget = Path.Combine(root, dependenciesDirectory);
            string packagesTarget = Path.Combine(dependenciesTarget, "packages");

            foreach (string directory in directories) {
                if (string.Equals(root, directory, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (IsReservedDirectory(directory)) {
                    continue;
                }

                string path = Path.Combine(directory, "packages");

                if (Directory.Exists(path) && new DirectoryInfo(path).Attributes == FileAttributes.ReparsePoint) {
                    logger.Debug("Symbolic link already present at path: {0}.", path);
                } else {
                    makeSymlinkTask.Link = path;
                    makeSymlinkTask.Target = packagesTarget;
                    makeSymlinkTask.FailIfLinkIsDirectoryWithContent = true;
                    makeSymlinkTask.ExecuteInternal(logger);
                }

                if (replicationEnabled) {
                    makeSymlinkTask.Target = dependenciesTarget;
                    makeSymlinkTask.FailIfLinkIsDirectoryWithContent = false;

                    path = Path.Combine(directory, "Dependencies");

                    if (Directory.Exists(path) && new DirectoryInfo(path).Attributes == FileAttributes.ReparsePoint) {
                        logger.Debug("Symbolic link already present at path: {0}.", path);
                        continue;
                    }

                    makeSymlinkTask.Link = path;
                    makeSymlinkTask.ExecuteInternal(logger);
                }
            }
        }
    }
}
