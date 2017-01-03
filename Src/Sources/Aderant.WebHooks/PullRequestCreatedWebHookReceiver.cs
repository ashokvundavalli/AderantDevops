using System.Threading.Tasks;
using Aderant.WebHooks.Actions;
using Microsoft.AspNet.WebHooks;
using Microsoft.AspNet.WebHooks.Payloads;

namespace Aderant.WebHooks {
    public class PullRequestCreatedWebHookReceiver : VstsWebHookHandlerBase, IWebHookHandler {
        public override Task ExecuteAsync(WebHookHandlerContext context, GitPullRequestCreatedPayload payload) {

            PullRequestLotto lotto = new PullRequestLotto(payload);
            lotto.AssignRandomPersonToPullRequest();
            

            return base.ExecuteAsync(context, payload);
        }
    }

    /*    public class PullRequestCreatedWebHookReceiver : VstsWebHookHandlerBase, IWebHookHandler {
        private readonly NetworkCredential credentials;   

        public override async Task ExecuteAsync(WebHookHandlerContext context, GitPullRequestCreatedPayload payload) {
            var connection = GetConnection();

            bool containsNonWebChanges = false;

            var resource = payload.Resource;
            using (var webClient = new WebClient() { Credentials = credentials }) {
                var sourceCommitJson = webClient.DownloadString(resource.Links.SourceCommit.Href);
                var sourceCommitResponse = JsonConvert.DeserializeObject<CommitResponse>(sourceCommitJson);

                var changesJson = webClient.DownloadString(sourceCommitResponse.Links.Changes.Href);
                var changesJsonResponse = JsonConvert.DeserializeObject<ChangesResponse>(changesJson);

                if (changesJsonResponse.Changes.Any(c => !c.Item.Path.Contains("Web."))) {
                    containsNonWebChanges = true;
                }
            }

            if (containsNonWebChanges) {
                var gitClient = connection.GetClient<GitHttpClient>();
                var pullRequest = await gitClient.GetPullRequestAsync(resource.Repository.Id, resource.PullRequestId);
                var reviewers = new List<IdentityRefWithVote>(pullRequest.Reviewers);

                var identityClient = connection.GetClient<IdentityHttpClient>();
                var timeIdentity1 = (await identityClient.ReadIdentitiesAsync(IdentitySearchFilter.DisplayName, "[ExpertSuite]\\\\Time and Collections")).FirstOrDefault();
                var timeIdentity2 = (await identityClient.ReadIdentitiesAsync(IdentitySearchFilter.Identifier, "ea79c4ae-a679-4a42-8ae5-6b3bee02e148")).FirstOrDefault();

                reviewers.Add(new IdentityRefWithVote() { }); //TODO: how to search for and add an identity?
                pullRequest.Reviewers = reviewers.ToArray();
                await gitClient.UpdatePullRequestAsync(pullRequest, resource.Repository.Id, resource.PullRequestId);
            }

            await base.ExecuteAsync(context, payload);
        }

        public override async Task ExecuteAsync(WebHookHandlerContext context, GitPullRequestUpdatedPayload payload) {
            var connection = GetConnection();

            bool isValid = true;

            var resource = payload.Resource;
            if (resource.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)) {
                using (var webClient = new WebClient() { Credentials = credentials }) {
                    if (resource.Links != null && resource.Links.WorkItems != null) {
                        var workItemListJson = webClient.DownloadString(resource.Links.WorkItems.Href);
                        var workItemListResponse = JsonConvert.DeserializeObject<WorkItemListResponse>(workItemListJson);
                        foreach (var workItemReference in workItemListResponse.Value) {
                            var workItemJson = webClient.DownloadString(workItemReference.Url);
                            var workItem = JsonConvert.DeserializeObject<WorkItemResponse>(workItemJson);

                            // Associated bugs have to be closed, maxChangeCount.e. tested
                            if (workItem.Fields.WorkItemType == "Bug" && workItem.Fields.State != "Closed") {
                                isValid = false;
                            } else if (workItem.Fields.WorkItemType == "User Story" && workItem.Fields.State != "Closed") {
                                isValid = false;
                            }
                        }
                    }
                }
            }

            if (!isValid) {
                var gitClient = connection.GetClient<GitHttpClient>();
                var pullRequest = await gitClient.GetPullRequestAsync(resource.Repository.Id, resource.PullRequestId);
                pullRequest.Status = PullRequestStatus.Active;
                pullRequest.MergeStatus = PullRequestAsyncStatus.RejectedByPolicy;
                await gitClient.UpdatePullRequestAsync(pullRequest, resource.Repository.Id, resource.PullRequestId);
            }

            await base.ExecuteAsync(context, payload);
        }

        public override Task ExecuteAsync(string receiver, WebHookHandlerContext context) {
            return base.ExecuteAsync(receiver, context);
        }

        private VssConnection GetConnection() {
            return new VssConnection(new Uri("http://tfs:8080/tfs/Aderant"), new VssCredentials(new WindowsCredential(credentials)));
        }
    }*/
}