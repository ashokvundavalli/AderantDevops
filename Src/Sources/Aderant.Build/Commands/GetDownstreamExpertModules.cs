using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Commands {
    [Cmdlet("Get", "DownstreamExpertModules")]
    public class GetDownstreamExpertModules : PSCmdlet {

        [Parameter(Mandatory = false, Position = 0)]
        public string ModuleName { get; set; }

        [Parameter(Mandatory = false, ValueFromPipeline = true, Position = 1)]
        public ExpertModule Module { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public string BranchPath { get; set; }

        protected override void ProcessRecord() {

            string branchPath = ParameterHelper.GetBranchPath(BranchPath, this.SessionState);

            if (ModuleName == null && Module == null) {
                throw new ArgumentException("Either Module or ModuleName must be specified");
            }

            DependencyBuilder builder = new DependencyBuilder(branchPath);

            Module = builder.GetAllModules().FirstOrDefault(x => x.Name.Equals(ModuleName, StringComparison.OrdinalIgnoreCase));

            if (Module == null) {
                throw new ArgumentException(string.Format("Could not find Module '{0}'", ModuleName));
            }

            var allDependencies = new List<ModuleDependency>(builder.GetModuleDependencies());

            HashSet<ExpertModule> modules = new HashSet<ExpertModule>();
            GetDependentModules(allDependencies, new List<ExpertModule> { Module }, modules);

            WriteObject(modules, true);
        }

        private List<ExpertModule> GetDirectDependencies(List<ModuleDependency> allDependencies, ExpertModule module) {
            var modules = new List<ExpertModule>();

            for (int i = 0; i < allDependencies.Count; i++) {
                var dependency = allDependencies[i];
                
                if (dependency.Provider.Equals(module)) {
                    if (!dependency.Consumer.Equals(module))
                        //yield return dependency.Consumer;
                        modules.Add(dependency.Consumer);
                }
            }

            return modules;
        }

        private void GetDependentModules(List<ModuleDependency> allDependencies, List<ExpertModule> directDependencies, HashSet<ExpertModule> modules) {
            for (int i = 0; i < directDependencies.Count; i++) {
                ExpertModule dependency = directDependencies[i];
                
                List<ExpertModule> nextDependencies = GetDirectDependencies(allDependencies, dependency);

                foreach (ExpertModule module in nextDependencies) {
                    modules.Add(module);
                }

                if (nextDependencies.Count == 0) {
                    continue;
                }

                GetDependentModules(allDependencies, nextDependencies, modules);
            }
        }
    }
}