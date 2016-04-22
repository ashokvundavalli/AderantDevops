using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Commands {
    [Cmdlet("Get", "ExpertBranches")]
    public class GetExpertBranches : PSCmdlet {

        [Parameter(Mandatory = false, Position = 0, HelpMessage = "Sets the starts with filter to apply to branch names")]
        public string Filter { get; set; }

        protected override void ProcessRecord() {
            string branchPath = ParameterHelper.GetBranchPath(null, SessionState);
            string branchName = GetBranchName();
            var branches = new List<string>();
            string rootPath = TempParameterHelper.GetExpertSuitePath(branchName, branchPath);
            string mainPath = Path.Combine(rootPath, "Main");
            if(Directory.Exists(mainPath) && IsModulePath(mainPath)) {
                branches.Add("Main");
            }
            var additionalBranches = from moduleGroup in Directory.GetDirectories(rootPath)
                                     from module in Directory.GetDirectories(moduleGroup)
                                     where IsModulePath(module)
                                     select
                                         string.Format("{0}\\{1}", new DirectoryInfo(moduleGroup).Name,
                                                       new DirectoryInfo(module).Name);
            branches.AddRange(additionalBranches);
            
            WriteObject(branches.OrderBy(x => x).Where(x => string.IsNullOrEmpty(Filter) || x.StartsWith(Filter, StringComparison.InvariantCultureIgnoreCase)).Distinct().ToArray(), true);
        }

        private static bool IsModulePath(string modulePath) {
            return File.Exists(Path.Combine(modulePath, @"Modules\Build.Infrastructure\Src\Package\ExpertManifest.xml"));
        }

        private string GetBranchName() {
            return SessionState.PSVariable.GetValue("BranchName", string.Empty).ToString();
        }
        
    }

    public static class TempParameterHelper {
        /// <summary>
        /// Gets the expert suite path.
        /// </summary>
        /// <param name="branchName">Name of the branch.</param>
        /// <param name="branchPath">The branch path.</param>
        /// <returns></returns>
        public static string GetExpertSuitePath(string branchName, string branchPath) {
            if(!Directory.Exists(branchPath)) {
                throw new DirectoryNotFoundException(branchPath);
            }
            return string.Join("\\", branchPath.TrimEnd('\\').Split('\\').Reverse().Skip(branchName.Trim('\\').Split('\\').Count()).Reverse().ToArray());
        }
    }
}
