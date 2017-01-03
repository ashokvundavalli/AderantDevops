using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aderant.WebHooks.Model;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Aderant.WebHooks {
    internal interface ITeamFoundationServer {
        IEnumerable<Contributor> GetGitContributors(Guid teamProjectId);

        void AddContributorToPullRequest(string repositoryId, int pullRequestId, Contributor reviewerToAdd);

        void AddCommentToPullRequest(string repositoryId, int pullRequestId, string comment);
    }
}