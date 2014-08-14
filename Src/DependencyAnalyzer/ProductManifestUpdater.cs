using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DependencyAnalyzer.Logging;
using DependencyAnalyzer.Providers;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;

namespace DependencyAnalyzer {
    /// <summary>
    /// Updates a Product Manifest (ExpertManifest.xml) against all modules on disk and referenced in Dependency Manifests
    /// </summary>
    public class ProductManifestUpdater {
        private readonly ILogger logger;
        private readonly IModuleProvider provider;

        /// <summary>
        /// Prevents a default instance of the <see cref="ProductManifestUpdater"/> class from being created.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="provider">The provider.</param>
        public ProductManifestUpdater(ILogger logger, IModuleProvider provider) {
            this.logger = logger;
            this.provider = provider;
        }

        /// <summary>
        /// Performs the update operation against the product manifest.
        /// </summary>
        /// <param name="sourceBranch">The source branch to source modules from.</param>
        /// <param name="targetBranch">The branch which should be updated with the new ExpertManifest.</param>
        public void Update(string sourceBranch, string targetBranch) {
            IEnumerable<ExpertModule> modules = provider.GetAll();

            sourceBranch = sourceBranch.Replace('/', Path.DirectorySeparatorChar);

            TfsTeamProjectCollection collection = TeamFoundation.GetTeamProjectServer();
            Workspace workspaceInfo = EditProductManifest(collection);

            IEnumerable<string> sourceBranchModules = GetSourceBranchModules(collection, sourceBranch);

            foreach (ExpertModule module in modules) {
                if (module.Name.Equals("Build.Infrastructure", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (ExpertModule.IsNonProductModule(module.ModuleType)) {
                    continue;
                }

                XElement element = AddOrUpdateExpertManifestEntry(provider.ProductManifest, module.Name);
                if (!provider.IsAvailable(module.Name)) {
                    string sourceBranchModule = sourceBranchModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase) ? sourceBranch : targetBranch;

                    if (!sourceBranch.Equals(targetBranch, StringComparison.OrdinalIgnoreCase)) {
                        // Module is not on disk or in the branch - use the sourceBranch if it exists there
                        AddOrUpdateAttribute(element, "GetAction", "branch");
                        AddOrUpdateAttribute(element, "Path", sourceBranchModule);
                    }
                } else {
                    XAttribute action = element.Attribute("GetAction");
                    if (action != null) {
                        action.Remove();
                    }

                    XAttribute path = element.Attribute("Path");
                    if (path != null) {
                        path.Remove();
                    }
                }

                if (module.ModuleType == ModuleType.ThirdParty) {
                    //Next item, as third party modules don't have dependency manifests
                    continue;
                }

                XDocument manifest;
                if (provider.TryGetDependencyManifest(module.Name, out manifest)) {
                    // since this module exists in our branch (the Test-Path test passed), remove any invalid attributes       
                    XAttribute action = element.Attribute("GetAction");
                    if (action != null) {
                        action.Remove();
                    }

                    XAttribute path = element.Attribute("Path");
                    if (path != null) {
                        path.Remove();
                    }

                    logger.Log("Synchronizing Expert Manifest against Dependency Manifest for: {0}", module.Name);

                    string dependencyManifestPath;
                    if (provider.TryGetDependencyManifestPath(module.Name, out dependencyManifestPath)) {
                        workspaceInfo.PendEdit(dependencyManifestPath);

                        // Add any dependencies of the module to the Expert Manifest
                        AddMissingModulesToExpertManifest(manifest, provider.ProductManifest);
                        SaveDocument(manifest, dependencyManifestPath);
                    }
                }
            }

            SaveDocument(provider.ProductManifest, provider.ProductManifestPath);
        }

        private Workspace EditProductManifest(TfsTeamProjectCollection collection) {
            var workspaceInfo = Workstation.Current.GetAllLocalWorkspaceInfo();
            foreach (WorkspaceInfo info in workspaceInfo) {
                Workspace workspace = info.GetWorkspace(collection);
                string path = workspace.TryGetServerItemForLocalItem(provider.ProductManifestPath);
                if (path != null) {
                    workspace.PendEdit(provider.ProductManifestPath);
                    return workspace;
                }
                
            }
            return null;
        }

        private void AddOrUpdateAttribute(XElement element, string attributeName, string attributeValue) {
            XAttribute attribute = element.Attribute(attributeName);
            if (attribute != null) {
                attribute.SetValue(attributeValue);
            } else {
                element.Add(new XAttribute(attributeName, attributeValue));
            }
        }

        private IEnumerable<string> GetSourceBranchModules(TfsTeamProjectCollection collection, string sourceBranch) {
            VersionControlServer service = collection.GetService<VersionControlServer>();
            string combine = VersionControlPath.Combine("$/ExpertSuite", sourceBranch + "/Modules");

            logger.Log(string.Empty);
            logger.Log("Getting module names from source branch: {0}", sourceBranch);
            logger.Log(string.Empty);

            ItemSet itemSet = service.GetItems(combine, VersionSpec.Latest, RecursionType.OneLevel, DeletedState.NonDeleted, ItemType.Folder, false);

            return itemSet.Items.Select(item => PathHelper.GetModuleName(item.ServerItem)).Where(m => m != null).ToList();
        }

        private void AddMissingModulesToExpertManifest(XDocument moduleManifest, XDocument productManifest) {
            IEnumerable<XElement> elements = moduleManifest.Descendants("ReferencedModule");

            foreach (XElement element in elements) {
                XAttribute action = element.Attribute("GetAction");
                if (action != null) {
                    action.Remove();
                }

                XAttribute path = element.Attribute("Path");
                if (path != null) {
                    path.Remove();
                }

                XAttribute nameAttribute = element.Attribute("Name");
                if (nameAttribute != null) {
                    AddOrUpdateExpertManifestEntry(productManifest, nameAttribute.Value);
                }
            }
        }

        private XElement AddOrUpdateExpertManifestEntry(XDocument manifest, string moduleName) {
            if (string.IsNullOrEmpty(moduleName)) {
                throw new ArgumentNullException("ModuleName cannot be null or empty for creating a new Expert Manifest entry");
            }

            var node = manifest.Root.Descendants("Module").FirstOrDefault(elm => elm.Attribute("Name").Value.Equals(moduleName, StringComparison.OrdinalIgnoreCase));

            if (node != null) {
                logger.Log("Found module {0} in Expert Manifest", moduleName);

                var attr = node.Attribute("AssemblyVersion");
                if (attr == null && !IsThirdParty(node)) {
                    logger.Warning("Non-third party module has no assembly version. Adding default");
                    node.Add(new XAttribute("AssemblyVersion", "1.8.0.0"));
                }

                return node;
            }

            if (ExpertModule.IsNonProductModule(ExpertModule.GetModuleType(moduleName))) {
                return null;
            }

            logger.Log("The Expert Manifest did not contain an entry for the module $moduleName. Adding...");

            XElement moduleElement = new XElement("Module");
            moduleElement.Add(new XAttribute("Name", moduleName));

            if (!IsThirdParty(moduleElement)) {
                moduleElement.Add(new XAttribute("AssemblyVersion", "1.8.0.0"));
            }

            manifest.Root.Element("Modules").Add(moduleElement);

            return moduleElement;
        }

        private static bool IsThirdParty(XElement element) {
            XAttribute xAttribute = element.Attribute("Name");
            return xAttribute != null && ExpertModule.GetModuleType(xAttribute.Value) == ModuleType.ThirdParty;
        }

        private static void SortManifestNodesByName(XElement productOrDependencyManifest) {
            XElement modules = productOrDependencyManifest.Element("Modules") ?? productOrDependencyManifest.Element("ReferencedModules");

            if (modules != null) {
                List<XElement> orderedEnumerable = modules.Descendants().OrderBy(o => o.Attribute("Name").Value).ToList();

                modules.RemoveAll();

                foreach (XElement element in orderedEnumerable) {
                    var attributes = element
                        .Attributes()
                        .OrderByDescending(a => a.Value.Equals("AssemblyVersion"))
                        .ThenByDescending(a => a.Value.Equals("GetAction"))
                        .ThenByDescending(a => a.Value.Equals("Path"))
                        .ToList();

                    element.RemoveAttributes();
                    foreach (XAttribute attribute in attributes) {
                        element.Add(attribute);
                    }

                    modules.Add(element);
                }
            }
        }

        private void SaveDocument(XDocument document, string path) {
            logger.Log("Saving manifest: {0}", path);

            SortManifestNodesByName(document.Root);

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "    ";
            settings.NewLineOnAttributes = false;
            settings.Encoding = Encoding.UTF8;

            using (XmlWriter writer = XmlWriter.Create(path, settings)) {
                document.Save(writer);
            }
        }
    }
}