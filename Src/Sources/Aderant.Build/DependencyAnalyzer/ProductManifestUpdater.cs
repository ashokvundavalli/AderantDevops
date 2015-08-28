using Aderant.Build.Providers;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;

namespace Aderant.Build.DependencyAnalyzer {
    internal class ProductManifestUpdater {
        private readonly ILogger logger;
        private readonly IModuleProvider provider;
        

        /// <summary>
        /// Initializes a new instance of the <see cref="ProductManifestUpdater"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="provider">The provider.</param>
        public ProductManifestUpdater(ILogger logger, IModuleProvider provider) {
            this.logger = logger;
            this.provider = provider;
        }

        public void Update(string sourceBranch, string targetBranch) {
            sourceBranch = PathHelper.GetBranch(sourceBranch);
            targetBranch = PathHelper.GetBranch(targetBranch);

            IEnumerable<ExpertModule> modules = provider.GetAll();

            sourceBranch = sourceBranch.Replace('/', Path.DirectorySeparatorChar);

            TfsTeamProjectCollection collection = TeamFoundationHelper.GetTeamProjectServer();
            VersionControlServer service = collection.GetService<VersionControlServer>();
            Workspace workspaceInfo = EditProductManifest();

            var validator = new SourceControlModuleInspector(logger, service);

            // The list of module names from the remote branch (eg Main)
            ICollection<string> sourceBranchModules = GetModulesFromSourceControl(service, sourceBranch);

            modules = AddModulesFromSourceControl(modules, targetBranch, service);
            
            IList<ExpertModule> removeList = new List<ExpertModule>();

            foreach (var module in modules) {
                SynchronizeProductManifestWithModules(module, workspaceInfo);
            }

            foreach (ExpertModule module in modules) {
                if (string.Equals(module.Name, "Build.Infrastructure", StringComparison.OrdinalIgnoreCase)) {
                    removeList.Add(module);
                    continue;
                }

                if (ExpertModule.IsNonProductModule(module.ModuleType)) {
                    // We don't want to remove these modules from the manifest as they were probably added 
                    // for a reason so we don't add them to the removeList here.
                    continue;
                }

                ExpertModule updatedModule = AddOrUpdateExpertManifestEntry(provider, module.Name);

                if (updatedModule != null) {
                    if (!provider.IsAvailable(module.Name)) {
                        // The module is not available in the current branch we need to check if it is available in the other branch
                        string sourceBranchModule = sourceBranchModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase) ? sourceBranch : targetBranch;

                        if (updatedModule.GetAction != GetAction.SpecificDropLocation && !validator.IsValidModule(module.Name, sourceBranch)) {
                            removeList.Add(module);
                            continue;
                        }

                        if (updatedModule.GetAction != GetAction.SpecificDropLocation && !sourceBranch.Equals(targetBranch, StringComparison.OrdinalIgnoreCase)) {
                            if (!string.Equals(updatedModule.Branch, sourceBranchModule)) {
                                updatedModule.GetAction = GetAction.Branch;
                                updatedModule.Branch = sourceBranchModule;
                            }
                        }
                    } else {
                        updatedModule.GetAction = GetAction.None;
                        updatedModule.Branch = null;
                    }
                }
            }

            provider.Remove(removeList);

            string expertManifestDocument = provider.Save();
            workspaceInfo.PendEdit(provider.ProductManifestPath);
            File.WriteAllText(provider.ProductManifestPath, expertManifestDocument);
        }

        private void SynchronizeProductManifestWithModules(ExpertModule module, Workspace workspaceInfo) {
            DependencyManifest manifest;
            if (provider.TryGetDependencyManifest(module.Name, out manifest)) {
                logger.Log("Synchronizing Expert Manifest against Dependency Manifest for: {0}", module.Name);

                string dependencyManifestPath;
                if (provider.TryGetDependencyManifestPath(module.Name, out dependencyManifestPath)) {
                    DependencyManifest dependencyManifest;

                    if (provider.TryGetDependencyManifest(module.Name, out dependencyManifest)) {
                        string instanceBeforeSave = File.ReadAllText(dependencyManifestPath);

                        AddMissingModulesToManifest(manifest.ReferencedModules, provider);

                        string modifiedManifestDocument = dependencyManifest.Save();

                        if (!string.Equals(instanceBeforeSave, modifiedManifestDocument)) {
                            workspaceInfo.PendEdit(dependencyManifestPath);
                            File.WriteAllText(dependencyManifestPath, modifiedManifestDocument);
                        }
                    }
                }
            }
        }

        private List<ExpertModule> AddModulesFromSourceControl(IEnumerable<ExpertModule> modules, string targetBranch, VersionControlServer vcs) {
            List<string> modulesMissingFromManifest = GetModulesMissingFromManifest(vcs, modules, targetBranch);
            List<ExpertModule> expertModules = new List<ExpertModule>(modules);

            string thirdPartyName = ModuleType.ThirdParty.ToString();

            if (modulesMissingFromManifest.Count > 0) {
                foreach (string name in modulesMissingFromManifest) {
                    if (!string.Equals(name, thirdPartyName, StringComparison.OrdinalIgnoreCase)) {
                        ExpertModule newModule = new ExpertModule {
                            Name = name
                        };
                        if (!ExpertModule.IsNonProductModule(newModule.ModuleType)) {
                            logger.Log("Adding module {0} found in source control to Expert Manifest: ", new string[] {
                                name
                            });
                            expertModules.Add(newModule);
                        }
                    }
                }
            }
            return expertModules;
        }

        private List<string> GetModulesMissingFromManifest(VersionControlServer vcs, IEnumerable<ExpertModule> modules, string branch) {
            List<string> moduleNames = GetModulesFromSourceControl(vcs, branch).ToList();
            moduleNames.RemoveAll(m => IsManifest(m, modules));

            return moduleNames;
        }

        private bool IsManifest(string name, IEnumerable<ExpertModule> moduleNames) {
            foreach (ExpertModule module in moduleNames) {
                if (string.Equals(name, module.Name, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        private Workspace EditProductManifest() {
            Workspace workspace = TeamFoundationHelper.GetWorkspaceForItem(provider.ProductManifestPath);
            
            string path = workspace.TryGetServerItemForLocalItem(provider.ProductManifestPath);
            if (path != null) {
                workspace.PendEdit(provider.ProductManifestPath);
                return workspace;
            }
            
            throw new InvalidOperationException("Could not determine current TFS workspace");
        }

        private ICollection<string> GetModulesFromSourceControl(VersionControlServer vcs, string branch) {
            logger.Log("Getting module names from source branch: {0}", branch);

            List<string> items = new List<string>();

            string[] paths = {
                "Modules",
                "Modules/ThirdParty"
            };

            string[] array = paths;

            for (int i = 0; i < array.Length; i++) {
                string path = array[i];
                string sccPath = VersionControlPath.Combine("$/ExpertSuite", branch + "/" + path);

                ItemSet itemSet = vcs.GetItems(sccPath, VersionSpec.Latest, RecursionType.OneLevel, DeletedState.NonDeleted, ItemType.Folder, false);

                items.AddRange(itemSet.Items.Select(item => PathHelper.GetModuleName(item.ServerItem)).Where(m => m != null));
            }
            return items.Distinct().ToList();
        }

        private void AddMissingModulesToManifest(IList<ExpertModule> referencedModules, IModuleProvider productManifest) {
            foreach (ExpertModule referencedModule in referencedModules) {
                ExpertModule matchingModule = productManifest.GetModule(referencedModule.Name);
                if (matchingModule == null) {
                    productManifest.Add(referencedModule);
                }
            }
        }

        private ExpertModule AddOrUpdateExpertManifestEntry(IModuleProvider manifest, string moduleName) {
            if (string.IsNullOrEmpty(moduleName)) {
                throw new ArgumentNullException("ModuleName cannot be null or empty for creating a new Expert Manifest entry");
            }

            ExpertModule node = manifest.GetModule(moduleName);
            ExpertModule result;

            if (node != null) {
                logger.Log("Found module {0} in Expert Manifest", moduleName);

                if (node.ModuleType != ModuleType.ThirdParty && string.IsNullOrEmpty(node.AssemblyVersion)) {
                    logger.Warning("Non-third party module has no assembly version. Adding default", null);
                    node.AssemblyVersion = "1.8.0.0";
                }

                return node;
            }

            if (ExpertModule.IsNonProductModule(ExpertModule.GetModuleType(moduleName))) {
                return null;
            }

            logger.Log("The Expert Manifest did not contain an entry for the module {0}. Adding...", moduleName);

            ExpertModule newModule = new ExpertModule {
                Name = moduleName,
                AssemblyVersion = "1.8.0.0"
            };
            manifest.Add(newModule);
            result = newModule;
            return result;
        }
    }
}