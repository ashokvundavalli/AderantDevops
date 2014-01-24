using System;
using System.Collections.Generic;
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

            Module =
                builder.GetAllModules().Where(
                    x => x.Name.Equals(ModuleName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if(Module == null) {
                throw new ArgumentException(string.Format("Could not find Module '{0}'", ModuleName));
            }

            IEnumerable<ModuleDependency> allDependencies = builder.GetModuleDependencies();

            ExpertModule[] downstreamModules = GetDependentModules(Module, allDependencies, 0).ToArray();

            WriteObject(downstreamModules, true);
        }

        protected virtual IEnumerable<ExpertModule> GetDependentModules(ExpertModule targetModule, IEnumerable<ModuleDependency> allDependencies, int depth) {
            IEnumerable<ExpertModule> firstLevelDependencies = from dependency in allDependencies
                                                               where dependency.Provider.Equals(targetModule)
                                                               select dependency.Consumer;
            if(depth > 20) {
                return firstLevelDependencies.Distinct();
            }
            return firstLevelDependencies.Concat(
                from dependency in firstLevelDependencies
                from deepDependency in GetDependentModules(dependency, allDependencies, depth + 1)
                select deepDependency
                ).Distinct();
            
        }
    }
}