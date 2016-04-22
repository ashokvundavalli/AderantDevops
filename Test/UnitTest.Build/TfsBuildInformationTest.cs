using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build;
using Aderant.Build.Providers;
using Aderant.Build.Tasks;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Common;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client.Catalog.Objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build {
    [TestClass]
    public class TfsInformationTypesTest {
        [TestMethod]
        public void SymStoreTransactionTests() {
            TfsTeamProjectCollection server = new TfsTeamProjectCollection(new Uri(@"http://tfs:8080/tfs/aderant"));
            server.EnsureAuthenticated();
            var service = server.GetService<IBuildServer>();
           // var buildAllSpec = service.CreateBuildDetailSpec("ExpertSuite", "Releases.803x.BuildAll");
           // var moduleBuild = service.CreateBuildDetailSpec("ExpertSuite", "Releases.803x.Libraries.SoftwareFactory");
           //// var queryResult = service.QueryBuilds(buildAllSpec);
            var detail = service.GetBuild(new Uri("vstfs:///Build/Build/171115"));

            var buildWarnings = InformationNodeConverters.GetBuildWarnings(detail);
            //   var TeamFoundationWorkspace = new ModuleWorkspace("$/ExpertSuite/Releases/803x/", TeamFoundationHelper.TeamFoundationServerUri, TeamFoundationHelper.TeamProject);
            //  TeamFoundationWorkspace.GetModulesWithPendingChanges(@"C:\tfs\ExpertSuite\Releases\803x\Modules");
        }
    }
}