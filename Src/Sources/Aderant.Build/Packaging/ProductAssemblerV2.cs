using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Aderant.Build.Packaging.NuGet;
using Newtonsoft.Json;

namespace Aderant.Build.Packaging {
    internal class ProductAssemblerV2 : IProductAssembler {
        private static readonly string[] illegalExtensions = new string[] {
            "._"
        };

        private static readonly string[] ignoredFiles = new[] {
            "acknowledgements", "license", "licence", "readme",

            // Custom files? Less well-known files? Should this be an external input?
            "Web.config.install.xdt", "Web.config.uninstall.xdt"
        };

        private readonly ILogger logger;
        private readonly Regex regex = new Regex(@"(net)\d+\\");
        private bool isLocalBuild;
        private ExpertManifest manifest;
        private SourceCodeInfo sourceCodeInfo;
        private string teamProject;
        private string tfsBuildId;
        private string tfsBuildNumber;
        private string tfvcBranch;
        private string tfvcSourceGetVersion;
        private VersionTracker versionTracker;

        public ProductAssemblerV2(string productManifestXml, ILogger logger) {
            this.logger = logger;
            this.manifest = ExpertManifest.Parse(productManifestXml);
        }

        public bool EnableVerboseLogging { get; set; }

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

            var operation = AssembleProduct(
                new ProductAssemblyContext {
                    Modules = modules.Select(m => manifest.GetModule(m.Name, m.DependencyGroup)).ToList(), BuildOutputs = buildOutputs, ProductDirectory = productDirectory
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
                var requirements = context.Modules.Select(DependencyRequirement.Create).ToList();
                DependencyRequirement.AssignGroup(requirements, true);
                manager.Add(requirements);
                manager.Restore();
            }

            var groups = context.Modules
                .Where(s => s.DependencyGroup != Constants.MainDependencyGroup)
                .Select(s => s.DependencyGroup)
                .Distinct(StringComparer.OrdinalIgnoreCase)
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

            var licenseText = CopyMainGroup(context, groups, workingDirectory, fs);

            foreach (var group in groups) {
                var path = Path.Combine(workingDirectory, "packages", group);
                var packages = fs.GetDirectories(path).ToList();

                if (!packages.Any()) {
                    throw new InvalidOperationException("There are no packages under path: " + path);
                }

                licenseText = licenseText.Concat(CopyPackageContentToProductDirectory(context, fs, packages, group));
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

        private IEnumerable<string> CopyMainGroup(ProductAssemblyContext context, string[] groups, string workingDirectory, IFileSystem fs) {
            var groupDirectories = groups.Select(g => Path.Combine(workingDirectory, "packages", g));

            var packages = fs.GetDirectories(Path.Combine(workingDirectory, "packages"))
                .ToList();

            // Remove all group directories so we don't double process them
            packages.RemoveAll(path => groupDirectories.Contains(path, PathUtility.PathComparer));

            // Remove well known package
            packages.RemoveAll(p => p.IndexOf("Aderant.Build.Analyzer", StringComparison.OrdinalIgnoreCase) >= 0);

            var licenseText = CopyPackageContentToProductDirectory(context, fs, packages, null);
            return licenseText;
        }

        private IEnumerable<string> CopyPackageContentToProductDirectory(ProductAssemblyContext context, IFileSystem fs, List<string> packages, string group) {
            ConcurrentBag<string> licenseText = new ConcurrentBag<string>();

            string[] nupkgEntries = new[] {
                "lib", "content"
            };

            foreach (string packageDirectory in packages) {
                ExpertModule module = context.GetModuleByPackage(packageDirectory, group);

                foreach (string packageDir in nupkgEntries) {
                    string nupkgDir = Path.Combine(packageDirectory, packageDir);

                    if (fs.DirectoryExists(nupkgDir)) {
                        string packageName = Path.GetFileName(packageDirectory);

                        if (module != null) {
                            if (context.RequiresContentProcessing(module)) {
                                RootItemHandler processor = new RootItemHandler(fs) {
                                    Module = module,
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

                        string relativeDirectory;
                        if (module != null) {
                            relativeDirectory = context.ResolvePackageRelativeDirectory(module);
                        } else {
                            relativeDirectory = Path.Combine(context.ProductDirectory, group ?? string.Empty);
                        }

                        if (module == null || module.ExcludeFromPackaging != true) {
                            ProcessDirectoryCopy(fs, nupkgDir, relativeDirectory, packageName.StartsWith("ThirdParty", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                }
            }

            return licenseText;
        }


        private void ProcessDirectoryCopy(IFileSystem fileSystem, string nupkgDir, string relativeDirectory, bool process) {
            string[] files = fileSystem.GetFiles(nupkgDir, "*", true).ToArray();

            List<PathSpec> pathSpecs = new List<PathSpec>(files.Length);

            logger.Info("Copying directory: '{0}' ==> '{1}'", nupkgDir, relativeDirectory);

            foreach (string file in files) {
                string extension = Path.GetExtension(file);
                if (extension != null && illegalExtensions.Contains(extension)) {
                    logger.Info($"Skipping file: '{file}' due to extension.");
                    continue;
                }

                if (ignoredFiles.Contains(Path.GetFileNameWithoutExtension(file), StringComparer.OrdinalIgnoreCase) || ignoredFiles.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)) {
                    logger.Info($"Skipping file: '{file}' due to name.");
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

            ActionBlock<PathSpec> fileCopy = BulkCopyModule(fileSystem, pathSpecs);
            fileCopy.Completion
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        private ActionBlock<PathSpec> BulkCopyModule(IFileSystem fileSystem, IEnumerable<PathSpec> pathSpecs) {
            try {
                return fileSystem.BulkCopy(pathSpecs, false, false, false);
            } catch (IOException ex) {
                logger.LogErrorFromException(ex, false, false);
                throw;
            }
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

        private class SourceCodeInfo {
            public string FileFormatVersion { get; set; }

            public TfvcInfo Tfvc { get; set; }

            public List<GitInfo> Git { get; set; }
        }

        private class TfvcInfo {
            public string TeamProject { get; set; }

            public string Branch { get; set; }

            public string ChangeSet { get; set; }

            public string BuildId { get; set; }

            public string BuildNumber { get; set; }
        }

        private class GitInfo {
            public string Repository { get; set; }

            public string Branch { get; set; }

            public string CommitHash { get; set; }

            public string BuildId { get; set; }

            public string BuildNumber { get; set; }

            public string PackageVersion { get; set; }
        }
    }
}