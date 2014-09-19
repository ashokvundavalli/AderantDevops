using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Xml.Linq;

namespace DependencyAnalyzer {
    [Cmdlet("Get", "ExpertModuleDependencies")]
    public class GetDependencies : PSCmdlet {
        
        [Parameter(Mandatory = false, Position = 0)]
        public string SourceModuleName { get; set; }

        [Parameter(Mandatory = false, ValueFromPipeline = true, Position = 1)]
        public ExpertModule SourceModule { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public SwitchParameter Recurse { get; set; }

        [Parameter(Mandatory = false, Position = 3)]
        public string BranchPath { get; set; }

        protected override void ProcessRecord() {
            string branchPath = ParameterHelper.GetBranchPath(BranchPath, this.SessionState);

            DependencyBuilder builder  = new DependencyBuilder(branchPath);

            if(SourceModuleName == null && SourceModule == null) {
                throw new ArgumentException("You must supply a SourceModule or SourceModuleName");
            }

            if(SourceModule == null) {
                SourceModule = builder.GetAllModules().Where(
                    x => x.Name.Equals(SourceModuleName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            }

            if(SourceModule == null) {
                throw new ArgumentException("SourceModuleName", string.Format("Could not find Module '{0}'", SourceModuleName));
            }

            if(!Recurse) {
                WriteObject((
                    from dependency in builder.GetModuleDependencies()
                    where dependency.Consumer.Equals(SourceModule)
                    select dependency.Provider).Distinct().ToArray(), true);
            }
            else {
                WriteObject(
                    GetModuleDependenciesRecursive(SourceModule, builder.GetModuleDependencies(), 0).Distinct().ToArray(), true);
            }
        }

        protected virtual IEnumerable<ExpertModule> GetModuleDependenciesRecursive(ExpertModule sourceModule, IEnumerable<ModuleDependency> allDependencies, int depth) {
            IEnumerable<ExpertModule> firstLevelDependencies = from dependency in allDependencies
                                                               where dependency.Consumer.Equals(sourceModule)
                                                               select dependency.Provider;
            if (depth > 20) {
                return firstLevelDependencies.Distinct();
            }
            return firstLevelDependencies.Concat(
                from dependency in firstLevelDependencies
                from deepDependency in GetModuleDependenciesRecursive(dependency, allDependencies, depth + 1)
                select deepDependency
                ).Distinct();

        }
    }
}