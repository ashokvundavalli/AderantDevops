﻿using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.WebHooks.Model;
using Microsoft.AspNet.WebHooks.Payloads;

namespace Aderant.WebHooks.Actions {
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
            
            AddLottoWinnerToPullRequest(recentContributors, connection, repositoryInfo);
        }

        private void AddLottoWinnerToPullRequest(IEnumerable<Contributor> recentContributors, ITeamFoundationServer connection, RepositoryInfo repositoryInfo) {
            var contributors = recentContributors.ToList();

            if (contributors.Any()) {
                var luckyPersonIndex = new Random().Next(contributors.Count);

                Contributor lottoWinner = contributors[luckyPersonIndex];

                connection.AddContributorToPullRequest(repositoryInfo.Id, Payload.Resource.PullRequestId, lottoWinner);
                connection.AddCommentToPullRequest(repositoryInfo.Id, Payload.Resource.PullRequestId, string.Format("{0} won the lottery and was added to this review.", lottoWinner.DisplayName));
            }
        }
    }
}