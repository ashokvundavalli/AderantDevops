using System;
using System.Collections.Generic;
using Aderant.WebHooks.Model;

namespace Aderant.TeamFoundation.Integration {
    internal interface ITeamFoundationServer {
        IEnumerable<Contributor> GetGitContributors(Guid teamProjectId);

        void AddContributorToPullRequest(string repositoryId, int pullRequestId, Contributor reviewerToAdd);

        void AddCommentToPullRequest(string repositoryId, int pullRequestId, string comment);
    }
}