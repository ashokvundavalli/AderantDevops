using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.WebHooks;
using Aderant.WebHooks.Model;
using Microsoft.AspNet.WebHooks.Payloads;

namespace Aderant.TeamFoundation.Integration.Actions {
    internal class PullRequestLotto : GitRepositoryActionBase<GitPullRequestCreatedPayload> {
        public PullRequestLotto(GitPullRequestCreatedPayload payload)
            : base(payload, payload.Resource.Repository) {
        }

        public void AssignRandomPersonToPullRequest() {
            var repositoryInfo = RepositoryInfo;

            var connection = GetTfsConnection(repositoryInfo);

            var service = new ContributionQueryService(connection);

            var recentContributors = service.GetRecentContributors(
                new RecentContributionCriteria {
                    TeamProjectId = new Guid(repositoryInfo.TeamProjectId),
                    SourceControlType = SourceControlType.Tfvc | SourceControlType.Git
                });

            List<Contributor> list = recentContributors.ToList();

            RemoveSelf(list);

            AddLottoWinnerToPullRequest(list, connection, repositoryInfo);
        }

        private void RemoveSelf(List<Contributor> list) {
            Guid id;
            if (Guid.TryParse(Payload.Resource.CreatedBy.Id, out id)) {
                list.RemoveAll(item => item.Id == id);
            }

            list.RemoveAll(item => string.Equals(item.DisplayName, Payload.Resource.CreatedBy.DisplayName, StringComparison.InvariantCultureIgnoreCase));
            list.RemoveAll(item => string.Equals(item.UniqueName, Payload.Resource.CreatedBy.UniqueName, StringComparison.InvariantCultureIgnoreCase));
        }

        private void AddLottoWinnerToPullRequest(List<Contributor> recentContributors, ITeamFoundationServer connection, RepositoryInfo repositoryInfo) {
            var contributors = recentContributors.ToList();

            if (contributors.Any()) {
                var luckyPersonIndex = new Random().Next(contributors.Count);

                Contributor lottoWinner = contributors[luckyPersonIndex];

                connection.AddContributorToPullRequest(repositoryInfo.Id, Payload.Resource.PullRequestId, lottoWinner);
                connection.AddCommentToPullRequest(repositoryInfo.Id, Payload.Resource.PullRequestId, $"{lottoWinner.DisplayName} won the lottery and was added to this review. Golf clap for {lottoWinner.DisplayName}.");
            }
        }
    }
}