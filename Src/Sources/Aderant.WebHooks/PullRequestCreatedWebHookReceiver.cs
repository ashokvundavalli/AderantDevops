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
}