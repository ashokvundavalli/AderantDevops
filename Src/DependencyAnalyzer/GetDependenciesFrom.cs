using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Framework.Build;
using DependencyAnalyzer.Providers;

namespace DependencyAnalyzer {

    [Cmdlet(VerbsCommon.Get, "DependenciesFrom")]
    public sealed class GetDependenciesFrom : PSCmdlet {
        [Parameter(Mandatory = false, Position = 0, HelpMessage = "Sets the module name or names which are the dependency providers.")]
        public string[] ProviderModules {
            get;
            set;
        }

        [Parameter(Mandatory = false, Position = 1, HelpMessage = "Sets the module names or names which are consuming the dependencies.")]
        public string[] ConsumerModules {
            get;
            set;
        }

        [Parameter(Mandatory = false, Position = 2, HelpMessage = "Flags that the dependency source or sources should be determined by your current pending changes.")]
        public SwitchParameter PendingChanges {
            get;
            set;
        }

        [Parameter(Mandatory = false, Position = 3)]
        public string[] AdditionalDestination {
            get;
            set;
        }


        // If changeset parameter is used this will contain the list of modules in the changeset.
        private List<ExpertModule> localModulesInPendingChanges = new List<ExpertModule>();


        protected override void ProcessRecord() {
            var processRecordStopwatch = Stopwatch.StartNew();
            base.ProcessRecord();

            if (PendingChanges) {
                localModulesInPendingChanges = GetLocalModulesInChangeSet();
            }
            string branchPath = ParameterHelper.GetBranchPath(null, SessionState);
            if (ConsumerModules == null || ConsumerModules.Length == 0) {
                ConsumerModules = new[] { ParameterHelper.GetCurrentModulePath(null, SessionState) };
            }
            // Canonicalize the module names to be paths
            for (int index = 0; index < ConsumerModules.Length; index++) {
                if (!ConsumerModules[index].StartsWith(branchPath, StringComparison.OrdinalIgnoreCase)) {
                    ConsumerModules[index] = Path.Combine(Path.Combine(branchPath, "Modules"), ConsumerModules[index]);
                }
                WriteDebug("ConsumerModule: " + ConsumerModules[index]);
            }

            var builder = new DependencyBuilder(branchPath);
            IEnumerable<Build> builds = builder.GetTree(true);

            foreach (string consumerModule in ConsumerModules) {
                string moduleName =
                    consumerModule.ToUpper()
                                .Replace(branchPath.ToUpper(), string.Empty)
                                .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                                .LastOrDefault();

                IEnumerable<ModuleDependency> moduleDependencies = FilterDependencies(builder, moduleName);

                foreach (
                    ModuleDependency dependency in
                        moduleDependencies.OrderByDescending(
                            o => builds.Where(b => b.Modules.Contains(o.Provider)).Select(b => b.Order).FirstOrDefault())) {
					// Check the dependency branch is this current branch.
                    if (dependency.Branch == null || branchPath.ToUpperInvariant().Contains(dependency.Branch.ToUpperInvariant())) {
                        if (dependency.Provider.ModuleType != ModuleType.ThirdParty &&
                            dependency.Provider.ModuleType != ModuleType.Build &&
                            dependency.Provider.ModuleType != ModuleType.Database) {
                            WriteCopyInfo(dependency);
                            // Get our two and from directories
                            string sourcePath = PathHelper.GetModuleOutputDirectory(branchPath, dependency.Provider);
                            string targetPath = PathHelper.GetModuleDependenciesDirectory(branchPath, dependency.Consumer);
                            if (!Directory.Exists(targetPath)) {
                                Directory.CreateDirectory(targetPath);
                            }
                            if (dependency.Provider.Name.ToUpper().Contains("WEB.")) {
                                // The source is a web module, these are packaged using webdeploy and need to be uzipped and copied in a specific way.
                                CallWebPackageExtract(targetPath, branchPath, dependency);
                            } else {
                                // Get a list of unique content, we don't want to dump all the stuff from bin into dependencies as we may drag out dated stuff along
                                List<FileInfo> content = ResolveUniqueBinContent(sourcePath,
                                                                                 PathHelper.GetModuleDependenciesDirectory(branchPath,
                                                                                                                                dependency
                                                                                                                                    .Provider));

                                foreach (FileInfo file in content) {
                                    var substringFromIndex = file.FullName.IndexOf("Bin\\Module", StringComparison.OrdinalIgnoreCase) +
                                                             "Bin\\Module".Length;

                                    string relativeFromBinPath = file.FullName.Substring(substringFromIndex).TrimStart(Path.DirectorySeparatorChar);
                                    string destination = Path.Combine(targetPath, relativeFromBinPath);
                                    CopyFile(destination, file);
                                    if (AdditionalDestination != null) {
                                        foreach (string additionalDestination in AdditionalDestination) {
                                            CopyFile(Path.Combine(additionalDestination, relativeFromBinPath), file);
                                        }
                                    }
                                }
                                if (dependency.Provider.Name.ToUpper().Contains("THIRDPARTY.") &&
                                    dependency.Consumer.Name.ToUpper().Contains("WEB.")) {
                                    // If we are getting a third party module and we are a Web module, then our dependencies need to go into additional directories
                                    // Not supported at the moment.  If you want third party modules you need to check them in and get in the usual way.
                                    // See line 176 of LoadDependencies.ps1 for this section of code.
                                    WriteWarning("This command cannot get third party modules for web projects.");
                                }
                            }
                        } else {
                            WriteWarning(string.Format("Cannot get {0}, this command cannot get third party, build or database modules.", dependency.Provider.Name));
                        }
					} // if the dependency branch is this current branch.
                }  // foreach dependency

                // Call WebDependencyCsprojSynchronize.exe to synch up folder and VS project.
                Host.UI.WriteLine(string.Format("Synchronizing web projectsin {0}.", moduleName));
                var csProjSynchStopwatch = Stopwatch.StartNew();
                var projectFileFolderSync = new ProjectFileFolderSync();
                string dependenciesFolderPath = Path.Combine(consumerModule, "Dependencies");
                WriteDebug("About to call projectFileFolderSync.Synchronize using the following params...");
                WriteDebug(string.Format("   dependenciesFolderPath = {0}", dependenciesFolderPath));
                projectFileFolderSync.Synchronize(dependenciesFolderPath);
                csProjSynchStopwatch.Stop();
                WriteDebug(string.Format("   Returned from projectFileFolderSync.Synchronize (took {0}ms)", csProjSynchStopwatch.ElapsedMilliseconds));

            } // for each target module

            processRecordStopwatch.Stop();
            Host.UI.WriteLine(string.Format("Get-DependencyFrom finished in {0}ms", processRecordStopwatch.ElapsedMilliseconds));


        }

        private void CallWebPackageExtract(string targetPath, string branchPath, ModuleDependency dependency) {
            // The source is a web module, these are packaged using webdeploy and need to be uzipped and copied in a specific way.
            Host.UI.WriteLine(string.Format("  Inserting {0} web assets into the {1} projects.", dependency.Provider.Name, dependency.Consumer.Name));
            var stopwatch = Stopwatch.StartNew();
            string destinationDependencyFolder = targetPath;
            string providerWebProjectRoot = PathHelper.Aggregate(branchPath,
                                                                         "Modules", dependency.Provider.Name,
                                                                         "Src", dependency.Provider.Name);
            string[] dependencies = Directory.GetFiles(
                PathHelper.Aggregate(branchPath, "Modules", dependency.Provider.Name, "Dependencies"),
                "*.*",
                SearchOption.AllDirectories
                ).OrderBy(s => s)
                                             .Select(
                                                 file =>
                                                 file.Substring(
                                                     file.IndexOf("Dependencies",
                                                                  StringComparison.InvariantCultureIgnoreCase) + 13))
                                             .ToArray();
            stopwatch.Stop();
            WriteDebug(string.Format("CallWebPackageExtract - calculated dependencies list in {0}ms", stopwatch.ElapsedMilliseconds));
            stopwatch = Stopwatch.StartNew();
            WriteDebug("About to call DeployWebDependenciesToProject using the following params...");
            WriteDebug(string.Format("   destinationDependencyFolder = {0}", destinationDependencyFolder));
            WriteDebug(string.Format("   folderToDeploy = {0}", providerWebProjectRoot));
            WriteDebug(string.Format("   moduleName = {0}", dependency.Provider.Name));
            WriteDebug(string.Format("   dependencies.Length = {0}", dependencies.Length));
            var webPackageExtract = new WebPackageExtract();
            webPackageExtract.DeployWebDependenciesToProject(destinationDependencyFolder, providerWebProjectRoot,
                                                             dependency.Provider.Name, dependencies);
            stopwatch.Stop();
            WriteDebug(string.Format("   Returned from DeployWebDependenciesToProject (took {0}ms)", stopwatch.ElapsedMilliseconds));
        }

        private void CopyFile(string destination, FileInfo file) {
            var newFile = new FileInfo(destination);
            if (newFile.Directory != null && !newFile.Directory.Exists) {
                newFile.Directory.Create();
            }

            if (file.Exists) {
                file.IsReadOnly = false;
                file.Refresh();
            }

            WriteDebug(string.Format("Copying {0} ==> {1}", file.FullName, newFile.FullName));
            file.CopyTo(newFile.FullName, true);
        }

        private IEnumerable<ModuleDependency> FilterDependencies(DependencyBuilder builder, string moduleName) {
            IEnumerable<ModuleDependency> dependencies = builder.GetModuleDependencies()
                .Where(moduleDependency => moduleDependency.Consumer.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));

            var moduleDependencies = new List<ModuleDependency>();
            if (PendingChanges) {
                WriteDebug("Filtering by change set");
                FilterDependenciesByChangeSet(moduleDependencies, dependencies);
            } else if(ProviderModules != null) {
                WriteDebug("Filtering by source modules");
                FilterDependenciesBySourceModules(moduleDependencies, dependencies);
            }

            return moduleDependencies;
        }

        private void FilterDependenciesBySourceModules(List<ModuleDependency> moduleDependencies, IEnumerable<ModuleDependency> dependencies) {
            foreach (string module in ProviderModules) {
                string name = module;
                moduleDependencies.AddRange(dependencies.Where(d => d.Provider.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private void FilterDependenciesByChangeSet(List<ModuleDependency> moduleDependencies, IEnumerable<ModuleDependency> dependencies) {
            foreach (ExpertModule module in localModulesInPendingChanges) {
                WriteDebug("Filter (module is in change set): " + module.Name);

                ExpertModule other = module;
                moduleDependencies.AddRange(dependencies.Where(d => d.Provider.Equals(other)));
            }
        }

        private List<ExpertModule> GetLocalModulesInChangeSet() {
            CommandInfo command = InvokeCommand.GetCommand("Get-ExpertModulesInChangeset", CommandTypes.Function);

            var modules = new List<ExpertModule>();
            if (command != null) {
                Collection<PSObject> results = InvokeCommand.InvokeScript(command.Name);

                foreach (PSObject psObject in results) {
                    ExpertModule module = psObject.BaseObject as ExpertModule;
                    modules.Add(module);
                }
            }
            return modules;
        }

        private void WriteCopyInfo(ModuleDependency dependency) {
            Host.UI.Write(ConsoleColor.Gray, Console.BackgroundColor, "Copying bin from ");
            Host.UI.Write(ConsoleColor.White, Console.BackgroundColor, "[");
            Host.UI.Write(ConsoleColor.DarkCyan, Console.BackgroundColor, dependency.Provider.Name);
            Host.UI.Write(ConsoleColor.White, Console.BackgroundColor, "]");
            Host.UI.Write(ConsoleColor.Gray, Console.BackgroundColor, " ==> ");
            Host.UI.Write(ConsoleColor.White, Console.BackgroundColor, "[");
            Host.UI.Write(ConsoleColor.Green, Console.BackgroundColor, dependency.Consumer.Name);
            Host.UI.Write(ConsoleColor.White, Console.BackgroundColor, "]");
            Host.UI.WriteLine();
        }

        private List<FileInfo> ResolveUniqueBinContent(string binDirectory, string dependenciesDirectory) {
            DirectoryInfo bin = new DirectoryInfo(binDirectory);
            DirectoryInfo dependencies = new DirectoryInfo(dependenciesDirectory);

            IEnumerable<FileInfo> binList = Enumerable.Empty<FileInfo>();
            if (bin.Exists) {
                binList = bin.GetFiles("*.*", SearchOption.AllDirectories);
            }

            IEnumerable<FileInfo> dependencyList = Enumerable.Empty<FileInfo>();
            if (dependencies.Exists) {
                dependencyList = dependencies.GetFiles("*.*", SearchOption.AllDirectories);
            }

            var myFileCompare = new FileCompare();

            return (from file in binList
                    select file).Except(dependencyList, myFileCompare).ToList();
        }
    }

    internal class FileCompare : IEqualityComparer<FileInfo> {
        public bool Equals(FileInfo f1, FileInfo f2) {
            return f1.Directory != null && (f2.Directory != null && (f1.Name == f2.Name && f1.Directory != null && f1.Directory.Parent == f2.Directory.Parent));
        }

        public int GetHashCode(FileInfo fi) {
            return fi.Name.GetHashCode();
        }
    }
}