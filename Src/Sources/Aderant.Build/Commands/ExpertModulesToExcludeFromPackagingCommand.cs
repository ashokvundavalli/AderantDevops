using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Xml;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;
using ModuleType = Aderant.Build.DependencyAnalyzer.ModuleType;

namespace Aderant.Build.Commands {
    [Cmdlet(VerbsCommon.Get, "ExpertModulesToExcludeFromPackaging")]
    public sealed class ExpertModulesToExcludeFromPackagingCommand : PSCmdlet {
        
        [Parameter(Position = 0)]
        public object Manifest { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        public string ExpertManifestPath { get; set; }


        protected override void ProcessRecord() {
            base.ProcessRecord();

            if (string.IsNullOrEmpty(ExpertManifestPath)) {
                throw new InvalidOperationException("Unable to filter modules as no path ExpertManifest.xml was provided");
            }

            ModuleWorkspace provider = new ModuleWorkspace(ExpertManifestPath, null, null);
            DependencyBuilder builder = provider.DependencyAnalyzer;

            List<ExpertModule> modules = builder.GetAllModules().ToList();
            List<ModuleDependency> allDependencies = builder.GetModuleDependencies(true).ToList();

            List<ExpertModule> modulesFromManifest = new List<ExpertModule>();

            if (Manifest != null) {
                GetModulesFromManifest(modules, modulesFromManifest);

                HashSet<ExpertModule> webThirdParty = new HashSet<ExpertModule>();
                HashSet<ExpertModule> thirdParty = new HashSet<ExpertModule>();

                foreach (var module in modulesFromManifest) {
                    if (module.ModuleType == ModuleType.ThirdParty || module.ModuleType == ModuleType.Help) {
                        continue;
                    }

                    ExpertModule[] dependencies = (from dependency in allDependencies
                                                   where dependency.Consumer.Equals(module)
                                                   select dependency.Provider).Distinct().ToArray();

                    if (module.ModuleType == ModuleType.Web) {
                        foreach (ExpertModule dependency in dependencies) {
                            if (dependency.ModuleType == ModuleType.ThirdParty) {
                                webThirdParty.Add(dependency);
                            }
                        }
                    } else {
                        foreach (ExpertModule dependency in dependencies) {
                            if (dependency.ModuleType == ModuleType.ThirdParty) {
                                thirdParty.Add(dependency);
                            }
                        }
                    }
                }

                // Find all of the unique web third party dependencies
                webThirdParty.ExceptWith(thirdParty);

                WriteObject(webThirdParty);
            }
        }

        private void GetModulesFromManifest(IEnumerable<ExpertModule> modules, List<ExpertModule> modulesFromManifest) {
            XmlNodeList nodeList;

            PSObject psobject = (Manifest as PSObject);
            if (psobject != null) {
                string path = psobject.BaseObject as string;
                XmlDocument sourceDoc = new XmlDocument();
                sourceDoc.Load(path);
                nodeList = sourceDoc.SelectNodes("//ProductManifest/Modules/Module");
            } else {
                nodeList = Manifest as XmlNodeList;
            }

            foreach (var entry in nodeList) {
                var element = entry as XmlElement;

                if (element != null) {
                    var value = element.GetAttribute("Name");

                    var module = modules.FirstOrDefault(m => string.Equals(m.Name, value, StringComparison.OrdinalIgnoreCase));
                    modulesFromManifest.Add(module);
                }
            }
        }
    }
}