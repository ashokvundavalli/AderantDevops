using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;
using ModuleType = Aderant.Build.DependencyAnalyzer.ModuleType;

namespace Aderant.Build.Commands {

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

        [Parameter(Mandatory = false, Position = 4, HelpMessage = "Flags that the dependency source dlls should be copied to the target Dependencies folder, in a Web Module.")]
        public SwitchParameter CopyWebDlls {
            get;
            set;
        }

        [Parameter(Mandatory = false, Position = 5, HelpMessage = "Flags that if csproj files will be modified with this file migration process.")]
        public SwitchParameter ExpressMode {
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
            if (!PendingChanges && (ProviderModules == null || ProviderModules.Length == 0)) {
                // ConsumerModule contains full path of the module, therefore we cannot do .StartWith comparisons
                if (ConsumerModules[0].IndexOf("Web.", StringComparison.OrdinalIgnoreCase) != -1) {
                    ProviderModules = ConsumerModules[0].IndexOf("Web.Presentation", StringComparison.OrdinalIgnoreCase) != -1
                        ? new[] { "Web.Foundation" }
                        : new[] { "Web.Foundation", "Web.Presentation" };
                } else {
                    WriteWarning("Provider Module(s) has to be specified.");
                    return;
                }
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
                string moduleName = GetModuleNameFromLocalPath(consumerModule, branchPath);
                IEnumerable<ModuleDependency> moduleDependencies = FilterDependencies(builder, moduleName);

                foreach (
                    ModuleDependency dependency in
                        moduleDependencies.OrderByDescending(
                            o => builds.Where(b => b.Modules.Contains(o.Provider)).Select(b => b.Order).FirstOrDefault())) {
                    // Check the dependency branch is this current branch.
                    if (dependency.Branch == null || branchPath.ToUpperInvariant().Contains(dependency.Branch.ToUpperInvariant())) {

                        if (string.Equals(dependency.Provider.Name, dependency.Consumer.Name, StringComparison.OrdinalIgnoreCase)) {
                            // if the dependency module is this current module.
                            WriteDebug(string.Format("Cannot get {0}, this command cannot get dependency from the same module.", dependency.Provider.Name));
                            continue;
                        }

                        if (dependency.Provider.ModuleType != ModuleType.ThirdParty &&
                                dependency.Provider.ModuleType != ModuleType.Build &&
                                dependency.Provider.ModuleType != ModuleType.Database) {
                            WriteCopyInfo(dependency);
                            // Get our to and from directories
                            string sourcePath = PathHelper.GetModuleOutputDirectory(branchPath, dependency.Provider);
                            string targetPath = PathHelper.GetModuleDependenciesDirectory(branchPath, dependency.Consumer);
                            if (!Directory.Exists(targetPath)) {
                                Directory.CreateDirectory(targetPath);
                            }

                                // Get a list of unique content, we don't want to dump all the stuff from bin into dependencies as we may drag out dated stuff along
                                List<FileInfo> content = ResolveUniqueBinContent(
                                    sourcePath,
                                    PathHelper.GetModuleDependenciesDirectory(
                                        branchPath,
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
                                if (dependency.Provider.Name.StartsWith("Web.", StringComparison.OrdinalIgnoreCase))
                                {
                                    // The source is a web module, these are packaged using webdeploy and need to be unzipped and copied in a specific way.
                                    dependency.Provider.Deploy(targetPath);
                                    //  CallWebPackageExtract(targetPath, branchPath, dependency);
                                }
                                if (dependency.Provider.Name.StartsWith("ThirdParty.", StringComparison.OrdinalIgnoreCase) &&
                                    dependency.Consumer.Name.StartsWith("Web.", StringComparison.OrdinalIgnoreCase)) {
                                    // If we are getting a third party module and we are a Web module, then our dependencies need to go into additional directories
                                    // Not supported at the moment.  If you want third party modules you need to check them in and get in the usual way.
                                    // See line 176 of LoadDependencies.ps1 for this section of code.
                                    WriteWarning("This command cannot get third party modules for web projects.");
                                }
                            
                        } else {
                            WriteWarning(string.Format("Cannot get {0}, this command cannot get third party, build or database modules.", dependency.Provider.Name));
                        }

                    } // if the dependency branch is this current branch.
                }  // foreach dependency
            } // for each target module

            processRecordStopwatch.Stop();
            Host.UI.WriteLine(string.Format("Get-DependencyFrom finished in {0}ms {1}",
                processRecordStopwatch.ElapsedMilliseconds,
                ExpressMode ? "in Express Mode, project files have not been modified." : "")
                );
        }

        private static string GetModuleNameFromLocalPath(string consumerModulePath, string branchPath) {
            return consumerModulePath.Substring(branchPath.Length).Split(Path.DirectorySeparatorChar).Last();
        }

        private void CopyFile(string destination, FileInfo file) {
            var newFile = new FileInfo(destination);
            if (newFile.Directory != null && !newFile.Directory.Exists) {
                newFile.Directory.Create();
            }

            if (newFile.Exists && newFile.IsReadOnly) {
                WriteWarning("Destination " + destination + " file is read only - skipped");
                return;
            }

            if (file.Exists) {
                file.IsReadOnly = false;
                file.Refresh();
            }

            WriteDebug(string.Format("Copying {0} ==> {1}", file.FullName, newFile.FullName));

            try {
                file.CopyTo(newFile.FullName, true);
            } catch (Exception ex) {
                WriteWarning("Copy failed. " + ex.Message);
            }
        }

        private IEnumerable<ModuleDependency> FilterDependencies(DependencyBuilder builder, string moduleName) {
            IEnumerable<ModuleDependency> dependencies = builder.GetModuleDependencies()
                .Where(moduleDependency => moduleDependency.Consumer.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));

            var moduleDependencies = new List<ModuleDependency>();
            if (PendingChanges) {
                WriteDebug("Filtering by change set");
                FilterDependenciesByChangeSet(moduleDependencies, dependencies);
            } else if (ProviderModules != null) {
                WriteDebug("Filtering by source modules");
                FilterDependenciesBySourceModules(moduleDependencies, dependencies);
            }

            return moduleDependencies;
        }

        private void FilterDependenciesBySourceModules(List<ModuleDependency> moduleDependencies, IEnumerable<ModuleDependency> dependencies) {
            foreach (string module in ProviderModules) {
                string name = module;
                moduleDependencies.AddRange(dependencies.Where(d => string.Equals(d.Provider.Name, name, StringComparison.OrdinalIgnoreCase)));
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