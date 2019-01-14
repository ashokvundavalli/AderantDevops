using System;
using System.Collections.Generic;
using System.Globalization;
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

            //string rootPath = TempParameterHelper.GetExpertSuitePath(branchName, branchPath);
            int position = branchPath.IndexOf(branchName, StringComparison.OrdinalIgnoreCase);
            var rootPath = branchPath.Substring(0, position);

            string mainPath = Path.Combine(rootPath, "Main");

            if (Directory.Exists(mainPath) && IsModulePath(mainPath)) {
                branches.Add("Main");
            }

            var additionalBranches = from moduleGroup in Directory.GetDirectories(rootPath)
                                     from module in Directory.GetDirectories(moduleGroup)
                                     where IsModulePath(module)
                                     select
                                         string.Format(CultureInfo.InvariantCulture, "{0}\\{1}", new DirectoryInfo(moduleGroup).Name,
                                                       new DirectoryInfo(module).Name);
            branches.AddRange(additionalBranches);
            
            WriteObject(branches.OrderBy(x => x).Where(x => string.IsNullOrEmpty(Filter) || x.StartsWith(Filter, StringComparison.InvariantCultureIgnoreCase)).Distinct().ToArray(), true);
        }

        private static bool IsModulePath(string modulePath) {
            return File.Exists(Path.Combine(modulePath, @"Modules\Build.Infrastructure\Src\Package\ExpertManifest.xml")) || File.Exists(Path.Combine(modulePath, "Modules", "ExpertManifest.xml"));
        }

        private string GetBranchName() {
            return SessionState.PSVariable.GetValue("BranchName", string.Empty).ToString();
        }
        
    }
}
