using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.WebHooks.Model;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Aderant.WebHooks {
    internal class ContributionQueryService {
        private readonly ITeamFoundationServer connection;

        public ContributionQueryService(ITeamFoundationServer connection) {
            this.connection = connection;
        }

        public IEnumerable<Contributor> GetRecentContributors(RecentContributionCriteria criteria) {
            HashSet<Contributor> identities = new HashSet<Contributor>();

            if (criteria.SourceControlType.HasFlag(SourceControlType.Tfvc)) {
                var contributors = GetTfvcContributors(criteria.TeamProjectId);

                foreach (var identity in contributors) {
                    identities.Add(identity);
                }
            }

            if (criteria.SourceControlType.HasFlag(SourceControlType.Git)) {
                var contributors = GetGitContributors(criteria.TeamProjectId);

                foreach (var identity in contributors) {
                    identities.Add(identity);
                }
            }

            return identities;
        }

        private IEnumerable<Contributor> GetGitContributors(Guid teamProjectId) {
            return connection.GetGitContributors(teamProjectId);
        }

        private IEnumerable<Contributor> GetTfvcContributors(Guid teamProjectId) {
            List<Contributor> contributors = new List<Contributor>();

            var server = connection as TeamFoundationServer;

            if (server != null) {
                List<TfvcChangesetRef> tfvcChangesetRefs =
                    server.GetLastestChangesetAsync(teamProjectId, 0, 1000).Result;

                var grouping = tfvcChangesetRefs.GroupBy(g => g.Author.Id);
                foreach (var group in grouping) {
                    TfvcChangesetRef changesetRef = group.First();

                    contributors.Add(new Contributor(changesetRef.Author.Id, changesetRef.Author.UniqueName, changesetRef.Author.DisplayName));
                }
            }

            return contributors;
        }
    }
}