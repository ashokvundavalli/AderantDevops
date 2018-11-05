using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;
using Aderant.Build.Logging;
using Aderant.Build.Packaging.NuGet;
using Newtonsoft.Json;

namespace Aderant.Build.Packaging {
    internal class ProductAssembler {
        private readonly ILogger logger;
        private ExpertManifest manifest;
        private VersionTracker versionTracker;
        private string tfvcSourceGetVersion;
        private string teamProject;
        private string tfvcBranch;
        private string tfsBuildId;
        private string tfsBuildNumber;
        private bool isLocalBuild;

        public ProductAssembler(string productManifestPath, ILogger logger) {
            this.logger = logger;
            this.manifest = ExpertManifest.Load(productManifestPath);
        }

        public IProductAssemblyResult AssembleProduct(
            IEnumerable<string> modules, 
            IEnumerable<string> buildOutputs, 
            string productDirectory, 
            string tfvcSourceGetVersion, 
            string teamProject, 
            string tfvcBranch, 
            string tfsBuildId,
            string tfsBuildNumber) {

            IEnumerable<ExpertModule> resolvedModules;
            if (modules != null) {
                resolvedModules = modules.Select(m => manifest.GetModule(m));
            } else {
                resolvedModules = Enumerable.Empty<ExpertModule>();
            }

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
                    Modules = resolvedModules,
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

            using (var manager = new PaketPackageManager(workingDirectory, fs, logger)) {
                manager.Add(context.Modules.Select(DependencyRequirement.Create));
                manager.Restore();
            }

            var packages = fs.GetDirectories(Path.Combine(workingDirectory, "packages")).ToArray();

            // hack
            packages = packages.Where(p => p.IndexOf("Aderant.Build.Analyzer", StringComparison.OrdinalIgnoreCase) == -1).ToArray();

            var licenseText = CopyPackageContentToProductDirectory(context, fs, packages);

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

        private IEnumerable<string> CopyPackageContentToProductDirectory(ProductAssemblyContext context, IFileSystem fs, string[] packages) {
            ConcurrentBag<string> licenseText = new ConcurrentBag<string>();

            SourceCodeInfo sourceCodeInfo = null;

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

            string[] nupkgEntries = new[] { "lib", "content" };

            foreach (var packageDirectory in packages) {
                ExpertModule module = context.GetModuleByPackage(packageDirectory);
                if (module == null) {
                    //throw new InvalidOperationException(string.Format("Unable to resolve module for path: {0}. The module should be defined in the product manifest.", packageDirectory));
                }

                foreach (var packageDir in nupkgEntries) {
                    string nupkgDir = Path.Combine(packageDirectory, packageDir);

                    if (fs.DirectoryExists(nupkgDir)) {
                        var packageName = Path.GetFileName(packageDirectory);

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
                            relativeDirectory = context.ProductDirectory;
                        }

                        logger.Info("Copying {0} ==> {1}", nupkgDir, relativeDirectory);
                        fs.CopyDirectory(nupkgDir, relativeDirectory);
                    }
                }
            }

            // write assembled information to file (for CI build)
            if (!isLocalBuild && sourceCodeInfo != null) {
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "CommitInfo.json"), JsonConvert.SerializeObject(sourceCodeInfo));
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "persist-build.ps1"), Resources.PersistBuildScript);
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "persist-build.bat"), Resources.PersistBuildBatch);
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "_readme.txt"), Resources.PersistBuildReadme);
                fs.WriteAllText(Path.Combine(context.ProductDirectory, "..", "..", "undo-buildpersistence.bat"), Resources.UndoBatch);
            }

            return licenseText;
        }

        private static void ReadLicenseText(IFileSystem fs, string lib, string packageName, ConcurrentBag<string> licenseText) {
            IEnumerable<string> licenses = fs.GetFiles(lib, "*license*txt", true);
            foreach (var licenseFile in licenses) {
                using (Stream stream = fs.OpenFile(licenseFile)) {
                    using (var reader = new StreamReader(stream)) {
                        licenseText.Add(new string('=', 80));
                        licenseText.Add(packageName);
                        licenseText.Add(new string('=', 80));
                        licenseText.Add(reader.ReadToEnd());
                    }
                }
            }
        }
    }
}
