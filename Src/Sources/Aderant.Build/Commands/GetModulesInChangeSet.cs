//using System;
//using System.Management.Automation;
//using Aderant.Build.Providers;
//using Microsoft.TeamFoundation.Client;

//namespace Aderant.Build.Commands {
//    [Cmdlet("Get", "GetModulesInChangeSet")]
//    public class GetModulesInChangeSet : Cmdlet {

//        public string BranchModulesDirectory { get; set; }

//        protected override void ProcessRecord() {
//            base.ProcessRecord();

//            var provider = new WorkspaceModuleProvider(TeamFoundationHelper.TeamFoundationServerUri, TeamFoundationHelper.TeamProject);
//            provider.GetModulesInPendingChanges(BranchModulesDirectory);
//        }
//    }
//}