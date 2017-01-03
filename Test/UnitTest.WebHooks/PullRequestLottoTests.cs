using System;
using Aderant.WebHooks;
using Aderant.WebHooks.Actions;
using Aderant.WebHooks.Model;
using Microsoft.AspNet.WebHooks.Payloads;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;

namespace UnitTest.WebHooks {
    [TestClass]
    public class PullRequestLottoTests {
        [TestMethod]
        public void Can_parse_tfs_details_from_event() {
            var request = JsonConvert.DeserializeObject<GitPullRequestCreatedPayload>(Resources.git_pullrequest_created);

            var repositoryInfo = new RepositoryInfo(request.Resource.Repository);

            Assert.AreEqual("ExpertSuite", repositoryInfo.TeamProject);
            Assert.AreEqual(new Uri("http://tfs.ap.aderant.com:8080/tfs/ADERANT/"), repositoryInfo.ServerUri);
        }

        [TestMethod]
        public void AddContributorToPullRequest_is_called() {
            var server = new Mock<ITeamFoundationServer>();
            server.Setup(s => s.GetGitContributors(It.IsAny<Guid>())).Returns(
                new [] {
                    new Contributor(Guid.NewGuid(), "AddContributorToPullRequest_is_called", "AddContributorToPullRequest_is_called"), 
                });
            
            var factory = new Mock<IServiceFactory>();
            factory.Setup(s => s.CreateTeamFoundationConnection<ITeamFoundationServer>(It.IsAny<Uri>())).Returns(server.Object);

            var request = JsonConvert.DeserializeObject<GitPullRequestCreatedPayload>(Resources.git_pullrequest_created);

            var lotto = new PullRequestLotto(request);
            lotto.ServiceFactory = factory.Object;

            lotto.AssignRandomPersonToPullRequest();

            server.Verify(s => s.AddContributorToPullRequest(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Contributor>()));
            server.Verify(s => s.AddCommentToPullRequest(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()));
        }
    }
}
