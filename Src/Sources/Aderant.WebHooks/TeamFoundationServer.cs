using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Aderant.WebHooks.Model;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.CodeReview.Discussion.WebApi;
using Microsoft.VisualStudio.Services.CodeReview.WebApi;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using Microsoft.VisualStudio.Services.WebApi;
using VssConnection = Microsoft.VisualStudio.Services.WebApi.VssConnection;

namespace Aderant.WebHooks {
    internal class TeamFoundationServer : ITeamFoundationServer {
        private static Dictionary<string, Identity> identityCache = new Dictionary<string, Identity>();

        private readonly VssConnection connection;

        public TeamFoundationServer() {
        }

        public TeamFoundationServer(VssConnection connection) {
            this.connection = connection;
        }

        public virtual Task<List<TfvcChangesetRef>> GetChangesetsAsync(Guid teamProjectId, int maxChangeCount, bool? includeDetails, int? maxCommentLength, bool? includeWorkItems, bool? includeSourceRename) {
            return connection.GetClient<TfvcHttpClient>()
                .GetChangesetsAsync(
                    teamProjectId,
                    maxChangeCount: maxChangeCount,
                    includeDetails: includeDetails,
                    includeWorkItems: includeWorkItems,
                    maxCommentLength: maxCommentLength,
                    includeSourceRename: includeSourceRename);
        }

        public virtual IEnumerable<Contributor> GetGitContributors(Guid teamProjectId) {
            var identities = GetRecentContributors(teamProjectId);

            var identityClient = connection.GetClient<IdentityHttpClient>();

            List<Identity> resolvedIdentities = new List<Identity>();
            foreach (var identity in identities) {
                Identity tfsIdentity;
                if (!identityCache.TryGetValue(identity.Name, out tfsIdentity)) {
                    var identitiesCollection = identityClient.ReadIdentitiesAsync(IdentitySearchFilter.MailAddress, identity.Email).Result;

                    tfsIdentity = identitiesCollection.FirstOrDefault();
                    if (tfsIdentity == null) {
                        identitiesCollection = identityClient.ReadIdentitiesAsync(IdentitySearchFilter.General, identity.Name).Result;
                        tfsIdentity = identitiesCollection.FirstOrDefault();
                    }

                    identityCache[identity.Name] = tfsIdentity;
                }

                if (tfsIdentity != null) {
                    if (resolvedIdentities.Any(ident => ident.Id == tfsIdentity.Id)) {
                        continue;
                    }

                    if (tfsIdentity.IsActive) {
                        resolvedIdentities.Add(tfsIdentity);
                    }
                }
            }

            return resolvedIdentities.Select(s => new Contributor(s.Id, string.Empty, s.DisplayName));
        }

        public virtual IEnumerable<GitUserDate> GetRecentContributors(Guid teamProjectId) {
            List<GitUserDate> identities = new List<GitUserDate>();

            var client = connection.GetClient<GitHttpClient>();
            var repositories = client.GetRepositoriesAsync(teamProjectId, false).Result;

            foreach (var repository in repositories) {
                var commits = client.GetCommitsAsync(
                    teamProjectId,
                    repository.Id,
                    new GitQueryCommitsCriteria {
                        FromDate = DateTime.UtcNow.AddDays(-90).ToString(CultureInfo.InvariantCulture),
                    }).Result;

                var authors = commits.Select(s => s.Author);

                foreach (var author in authors) {
                    if (identities.Any(ident => string.Equals(ident.Name, author.Name))) {
                        continue;
                    }
                    identities.Add(author);
                }
            }

            return identities;
        }

        public virtual void AddContributorToPullRequest(string repositoryId, int pullRequestId, Contributor reviewerToAdd) {
            var gitClient = connection.GetClient<GitHttpClient>();
            var pullRequest = gitClient.GetPullRequestAsync(repositoryId, pullRequestId).Result;

            var reviewer = new IdentityRefWithVote();

            reviewer.Id = reviewerToAdd.Id.ToString();
            reviewer.IsRequired = false;
            reviewer.Vote = (short)ReviewerVote.None;

            if (!string.IsNullOrEmpty(reviewerToAdd.DisplayName)) {
                reviewer.DisplayName = reviewerToAdd.DisplayName;
            }

            if (!string.IsNullOrEmpty(reviewerToAdd.UniqueName)) {
                reviewer.UniqueName = reviewerToAdd.UniqueName;
            }

            var list = pullRequest.Reviewers.ToList();
            list.Add(reviewer);

            // UpdatePullRequestAsync and CreatePullRequestReviewerAsync methods just don't work
            // UpdatePullRequestAsync - throws an exception Parameter name: You can only update reviewers, descriptions, titles, merge status, and status.
            // CreatePullRequestReviewerAsync - throws an exception, you must provide a valid reviewer.
            try {
                var result = gitClient.CreatePullRequestReviewersAsync(list.ToArray(), pullRequest.Repository.Id, pullRequest.PullRequestId).Result;
            } catch {
            }
        }

        public virtual void AddCommentToPullRequest(string repositoryId, int pullRequestId, string comment) {
            var gitClient = connection.GetClient<GitHttpClient>();
            var pullRequest = gitClient.GetPullRequestAsync(repositoryId, pullRequestId).Result;

            var postComments = new PostComments(connection, pullRequest);
            postComments.AddNewThreadWithComment(comment);
        }
    }

    internal class PostComments {
        private readonly VssConnection connection;

        public PostComments(VssConnection connection, GitPullRequest pullRequest) {
            this.connection = connection;
            this.ArtifactUrl = GetArtifactUrl(pullRequest.CodeReviewId, pullRequest.Repository.ProjectReference.Id, pullRequest.PullRequestId);
        }

        public string ArtifactUrl { get; private set; }

        private string GetArtifactUrl(int codeReviewId, Guid teamProjectId, int pullRequestId) {
            if (codeReviewId == 0) {
                return string.Format("vstfs:///CodeReview/CodeReviewId/{0}%2f{1}", teamProjectId, pullRequestId);
            }

            return CodeReviewSdkArtifactId.GetArtifactUri(teamProjectId, codeReviewId);
        }

        public void AddNewThreadWithComment(string content) {
            var thread = new ArtifactDiscussionThread {
                DiscussionId = -1,
                ArtifactUri = ArtifactUrl,
                Status = DiscussionStatus.Unknown,
                Properties = AddThreadProperties()
            };

            var comment = new DiscussionComment {
                DiscussionId = thread.DiscussionId,
                CanDelete = false,
                IsDeleted = false,
                Content = content,
                PublishedDate = DateTime.UtcNow,
                CommentType = CommentType.System,
            };

            thread.Comments = new[] {
                comment
            };

            var service = connection.GetClient<DiscussionHttpClient>();

            try {
                var result = service.CreateThreadAsync(thread).Result;
            } catch {
                
            }
        }

        private PropertiesCollection AddThreadProperties() {
            return new PropertiesCollection(
                new Dictionary<string, object> {
                    { "Microsoft.TeamFoundation.Discussion.SupportsMarkdown", true }
                });
        }
    }
}