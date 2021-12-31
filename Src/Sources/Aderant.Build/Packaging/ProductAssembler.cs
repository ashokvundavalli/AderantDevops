using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Aderant.Build.Packaging.NuGet;
using Newtonsoft.Json;

namespace Aderant.Build.Packaging {
    internal class ProductAssembler : IProductAssembler {
        private readonly ILogger logger;
        private VersionTracker versionTracker;
        private string tfvcSourceGetVersion;
        private string teamProject;
        private string tfvcBranch;
        private string tfsBuildId;
        private string tfsBuildNumber;
        private bool isLocalBuild;
        private SourceCodeInfo sourceCodeInfo;
        private readonly ExpertManifest manifest;
        public bool EnableVerboseLogging { get; set; }

        public ProductAssembler(ExpertManifest expertManifest, ILogger logger) {
            this.logger = logger;
            this.manifest = expertManifest;
        }

        public IProductAssemblyResult AssembleProduct(
            IReadOnlyCollection<ExpertModule> modules,
            IEnumerable<string> buildOutputs,
            string productDirectory,
            string tfvcSourceGetVersion,
            string teamProject,
            string tfvcBranch,
            string tfsBuildId,
            string tfsBuildNumber) {

            // the additional TFS info will only be passed in a CI build
            if (string.IsNullOrEmpty(tfvcBranch) && string.IsNullOrEmpty(tfvcSourceGetVersion) && string.IsNullOrEmpty(teamProject) && string.IsNullOrEmpty(tfsBuildId) && string.IsNullOrEmpty(tfsBuildNumber)) {
                this.isLocalBuild = true;
            } else {
                this.tfvcSourceGetVersion = tfvcSourceGetVersion;
                this.teamProject = teamProject;
                this.tfvcBranch = tfvcBranch;
                this.tfsBuildId = tfsBuildId;
                this.tfsBuildNumber = tfsBuildNumber;
            }

            var operation = AssembleProduct(new ProductAssemblyContext {
                Modules = modules.Select(m => manifest.GetModule(m.Name, m.DependencyGroup)).ToList(),
                BuildOutputs = buildOutputs,
                ProductDirectory = productDirectory
            });

            return operation;
        }

        private IProductAssemblyResult AssembleProduct(ProductAssemblyContext context) {
            versionTracker = new VersionTracker();

            RetrieveBuildOutputs(context);

            IEnumerable<string> licenseText = RetrievePackages(context);

            return new ProductAssemblyResult {
                ThirdPartyLicenses = licenseText.ToList()
            };
        }

        private void RetrieveBuildOutputs(ProductAssemblyContext context) {
            var fs = new PhysicalFileSystem();

            foreach (var folder in context.BuildOutputs) {
                logger.Info("Copying {0} ==> {1}", folder, context.ProductDirectory);

                fs.CopyDirectory(folder, context.ProductDirectory);
            }
        }

        private IEnumerable<string> RetrievePackages(ProductAssemblyContext context) {
            var workingDirectory = Path.Combine(context.ProductDirectory, "package." + Path.GetRandomFileName());

            var fs = new RetryingPhysicalFileSystem();

            using (var manager = new PaketPackageManager(workingDirectory, fs, WellKnownPackageSources.Default, logger, EnableVerboseLogging)) {
                manager.Add(context.Modules.Select(DependencyRequirement.Create));
                manager.Restore();
            }

            var groups = context.Modules
                .Where(s => s.DependencyGroup != Constants.MainDependencyGroup)
                .Select(s => s.DependencyGroup)
                .ToArray();


            // assemble information about source code for CI build
            if (!isLocalBuild) {
                sourceCodeInfo = new SourceCodeInfo {
                    FileFormatVersion = "1.0", // in case the format of this info changes at some later stage and we need to distinguish between them
                    Tfvc = new TfvcInfo {
                        Branch = tfvcBranch,
                        ChangeSet = tfvcSourceGetVersion,
                        TeamProject = teamProject,
                        BuildId = tfsBuildId,
                        BuildNumber = tfsBuildNumber
                    },
                    Git = new List<GitInfo>()
                };
            }

            var packages = fs.GetDirectories(Path.Combine(workingDirectory, "packages")).ToArray();
            packages = packages.Where(p => p.IndexOf("Aderant.Build.Analyzer", StringComparison.OrdinalIgnoreCase) == -1).ToArray();

            var licenseText = CopyPackageContentToProductDirectory(context, fs, packages, null);

            foreach (var group in groups) {
                var path = Path.Combine(workingDirectory, "packages", group);
                packages = fs.GetDirectories(path).ToArray();

                if (!packages.Any()) {
                    throw new InvalidOperationException("There are no packages under path: " + path);
                }

                CopyPackageContentToProductDirectory(context, fs, packages, group);
            }

            // write assembled information to file (for CI build)
            if (!isLocalBuild && sourceCodeInfo != null) {
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "CommitInfo.json"), JsonConvert.SerializeObject(sourceCodeInfo));
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "persist-build.ps1"), Resources.PersistBuildScript);
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "persist-build.bat"), Resources.PersistBuildBatch);
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "_readme.txt"), Resources.PersistBuildReadme);
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "undo-buildpersistence.bat"), Resources.UndoBatch);
            }

            fs.DeleteDirectory(workingDirectory, true);

            return licenseText;
        }

        private class SourceCodeInfo {
            public string FileFormatVersion {
                get; set;
            }
            public TfvcInfo Tfvc {
                get; set;
            }
            public List<GitInfo> Git {
                get; set;
            }
        }

        private class TfvcInfo {
            public string TeamProject {
                get; set;
            }
            public string Branch {
                get; set;
            }
            public string ChangeSet {
                get; set;
            }
            public string BuildId {
                get; set;
            }
            public string BuildNumber {
                get; set;
            }
        }

        private class GitInfo {
            public string Repository {
                get; set;
            }
            public string Branch {
                get; set;
            }
            public string CommitHash {
                get; set;
            }
            public string BuildId {
                get; set;
            }
            public string BuildNumber {
                get; set;
            }
            public string PackageVersion {
                get; set;
            }
        }

        private IEnumerable<string> CopyPackageContentToProductDirectory(ProductAssemblyContext context, IFileSystem fs, string[] packages, string group) {
            ConcurrentBag<string> licenseText = new ConcurrentBag<string>();

            string[] nupkgEntries = new[] { "lib", "content" };

            var powerShell = new PowerShellPipelineExecutor();

            foreach (string packageDirectory in packages) {
                ExpertModule module = context.GetModuleByPackage(packageDirectory, group);

                if (module?.PostProcess != null) {
                    // Set working directory.
                    string script = Path.Combine(Path.GetDirectoryName(manifest.ProductManifestPath), module.PostProcess);

                    if (!fs.FileExists(script)) {
                        throw new FileNotFoundException($"Post process script for module: '{module.Name}' does not exist.", script);
                    }

                    PSCommand command = new PSCommand();
                    command.AddScript(fs.ReadAllText(script));

                    logger.Info("Running post-process script: '{0}'", script);

                    var directories = context.ResolvePackageRelativeDestinationDirectories(module);

                    foreach (string directory in directories) {
                        powerShell.RunScript(command, new Dictionary<string, object>(1) { { "Package", packageDirectory } }, directory);
                    }
                }

                foreach (string packageDir in nupkgEntries) {
                    string nupkgDir = Path.Combine(packageDirectory, packageDir);

                    if (fs.DirectoryExists(nupkgDir)) {
                        string packageName = Path.GetFileName(packageDirectory);

                        if (module != null && module.PostProcess == null) {
                            if (context.RequiresContentProcessing(module)) {
                                RootItemHandler processor = new RootItemHandler(fs) {
                                    Module = module
                                };

                                processor.MoveContent(context, nupkgDir);

                                versionTracker.FileSystem = fs;
                                versionTracker.RecordVersion(module, packageDirectory);
                                continue;
                            }
                        }

                        // add Git repo source code info (for CI build)
                        if (!isLocalBuild) {
                            foreach (string file in fs.GetFiles(fs.GetFullPath(packageDirectory), "*.nuspec", true)) {

                                logger.Info("Analyzing {0}", file);

                                string text;
                                using (var stream = fs.OpenFile(fs.GetFullPath(file))) {
                                    using (var reader = new StreamReader(stream)) {
                                        text = reader.ReadToEnd();
                                    }
                                }

                                var branchName = NuspecSerializer.GetBranchName(text);
                                var commitHash = NuspecSerializer.GetCommitHash(text);
                                var repositoryName = NuspecSerializer.GetRepositoryName(text);
                                var buildId = NuspecSerializer.GetBuildId(text);
                                var buildNumber = NuspecSerializer.GetBuildNumber(text);
                                var packageVersion = NuspecSerializer.GetVersion(text);

                                if (sourceCodeInfo != null && !string.IsNullOrEmpty(branchName) && !string.IsNullOrEmpty(commitHash) && !string.IsNullOrEmpty(buildId)) {
                                    logger.Info("Last commit of {0} ({1}) was {2} for build {3}", repositoryName, branchName, commitHash, buildId);

                                    if (!sourceCodeInfo.Git.Any(g => g.Repository == repositoryName && g.Branch == branchName && g.BuildId == buildId)) {
                                        sourceCodeInfo.Git.Add(
                                            new GitInfo {
                                                Branch = branchName,
                                                CommitHash = commitHash,
                                                Repository = repositoryName,
                                                BuildId = buildId,
                                                BuildNumber = buildNumber,
                                                PackageVersion = packageVersion
                                            });
                                    }
                                }
                            }
                        }

                        ReadLicenseText(fs, nupkgDir, packageName, licenseText);

                        if (module == null || module.ExcludeFromPackaging != true && module?.PostProcess == null) {
                            string relativeDirectory;
                            if (module != null) {
                                relativeDirectory = context.ResolvePackageRelativeDirectory(module);
                            } else {
                                relativeDirectory = Path.Combine(context.ProductDirectory, group ?? string.Empty);
                            }

                            ProcessDirectoryCopy(fs, nupkgDir, relativeDirectory, packageName.StartsWith("ThirdParty", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                }
            }

            return licenseText;
        }

        private static readonly string[] IllegalExtensions = new string[] {
            "._"
        };

        private void ProcessDirectoryCopy(IFileSystem fileSystem, string nupkgDir, string relativeDirectory, bool process) {
            Regex regex = new Regex(@"(net)\d+\\");

            string[] files = fileSystem.GetFiles(nupkgDir, "*", true).ToArray();

            List<PathSpec> pathSpecs = new List<PathSpec>(files.Length);

            logger.Info("Copying directory: '{0}' ==> '{1}'", nupkgDir, relativeDirectory);

            foreach (string file in files) {
                string extension = Path.GetExtension(file);
                if (extension != null && IllegalExtensions.Contains(extension)) {
                    logger.Info($"Skipping file: '{file}' due to extension.");
                    continue;
                }

                string relativeFilePath = file.Replace(nupkgDir, string.Empty);
                if (process && regex.IsMatch(relativeFilePath)) {
                    relativeFilePath = Path.Combine(regex.Replace(relativeFilePath, string.Empty, 1));
                }

                PathSpec pathSpec = new PathSpec(file, Path.Combine(relativeDirectory, relativeFilePath.TrimStart(Path.DirectorySeparatorChar)), false);

                logger.Info($"Source file: '{pathSpec.Location}' ==> Destination: '{pathSpec.Destination}'");

                pathSpecs.Add(pathSpec);
            }

            ActionBlock<PathSpec> fileCopy = BulkCopyModule(pathSpecs);
            fileCopy.Completion
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public ActionBlock<PathSpec> BulkCopyModule(IEnumerable<PathSpec> pathSpecs) {
            ExecutionDataflowBlockOptions actionBlockOptions = new ExecutionDataflowBlockOptions {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
            };

            const int hResult = -2147024816; // File already exists.
            HashSet<string> knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ActionBlock<PathSpec> bulkCopy = new ActionBlock<PathSpec>(
                async file => {
                    // Break from synchronous thread context of caller to get onto thread pool thread.
                    await Task.Yield();

                    string destination = Path.GetDirectoryName(file.Destination);

                    lock (knownPaths) {
                        if (!knownPaths.Contains(destination)) {
                            if (!Directory.Exists(destination)) {
                                Directory.CreateDirectory(destination);
                            }

                            knownPaths.Add(destination);
                        }
                    }

                    try {
                        File.Copy(file.Location, file.Destination, false);
                    } catch (IOException exception) {
                        if (exception.HResult == hResult) {
                            // Log a warning if the file already exists.
                            logger.Warning(exception.Message);
                        } else {
                            throw;
                        }
                    }
                },
                actionBlockOptions);

            foreach (PathSpec pathSpec in pathSpecs) {
                bulkCopy.Post(pathSpec);
            }

            bulkCopy.Complete();

            return bulkCopy;
        }

        private static void ReadLicenseText(IFileSystem fs, string lib, string packageName, ConcurrentBag<string> licenseText) {
            IEnumerable<string> licenses = fs.GetFiles(lib, "*license*txt", true);
            string separator = new string('=', 80);

            foreach (string licenseFile in licenses) {
                using (Stream stream = fs.OpenFile(licenseFile)) {
                    using (StreamReader reader = new StreamReader(stream)) {
                        licenseText.Add(separator);
                        licenseText.Add(packageName);
                        licenseText.Add(separator);
                        licenseText.Add(reader.ReadToEnd());
                    }
                }
            }
        }
    }

    internal interface IProductAssembler {
        bool EnableVerboseLogging { get; set; }

        IProductAssemblyResult AssembleProduct(
            IReadOnlyCollection<ExpertModule> modules,
            IEnumerable<string> buildOutputs,
            string productDirectory,
            string tfvcSourceGetVersion,
            string teamProject,
            string tfvcBranch,
            string tfsBuildId,
            string tfsBuildNumber);
    }
}