//using System;
//using System.IO;
//using Microsoft.TeamFoundation.Build.Client;
//using Microsoft.TeamFoundation.Build.Common;
//using Microsoft.TeamFoundation.Client;
//using Microsoft.VisualStudio.TestTools.UnitTesting;

//namespace UnitTest.Build {
//    [TestClass]
//    public class TfsInformationTypesTest {
//        [TestMethod]
//        public void SymStoreTransactionTests() {
//            TfsTeamProjectCollection server = new TfsTeamProjectCollection(new Uri(@"http://tfs:8080/tfs/aderant"));
//            server.EnsureAuthenticated();

//            var currentBuild1 = server.GetService<IBuildServer>().GetBuild(new Uri(@"vstfs:///Build/Build/150647"));
//            var buildInformation1 = currentBuild1.Information;
//            var buildInformationNodes1 = buildInformation1.GetNodesByType(InformationTypes.SymStoreTransaction);
//        }
//    }
//}