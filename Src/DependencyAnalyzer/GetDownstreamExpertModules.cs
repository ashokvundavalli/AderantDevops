using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace DependencyAnalyzer {
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

            IEnumerable<ModuleDependency> allDependencies = builder.GetModuleDependencies();

            HashSet<ExpertModule> modules = new HashSet<ExpertModule>();
            GetDependentModules(allDependencies, new Collection<ExpertModule> { Module }, modules);

            WriteObject(modules, true);
        }

        private IEnumerable<ExpertModule> GetDirectDependencies(IEnumerable<ModuleDependency> allDependencies, ExpertModule module) {
            return from dependency in allDependencies
                   where dependency.Provider.Equals(module)
                   where !dependency.Consumer.Equals(module)
                   select dependency.Consumer;
        }

        private void GetDependentModules(IEnumerable<ModuleDependency> allDependencies, IEnumerable<ExpertModule> directDependencies, HashSet<ExpertModule> modules) {
            foreach (ExpertModule dependency in directDependencies) {
                var nextDependencies = GetDirectDependencies(allDependencies, dependency);

                foreach (ExpertModule module in nextDependencies) {
                    modules.Add(module);
                }

                if (!nextDependencies.Any()) {
                    continue;
                }

                GetDependentModules(allDependencies, nextDependencies, modules);
            }
        }
    }
}