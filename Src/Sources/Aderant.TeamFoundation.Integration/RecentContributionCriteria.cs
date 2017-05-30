using System;
using Aderant.WebHooks.Model;

namespace Aderant.TeamFoundation.Integration {
    internal class RecentContributionCriteria {
        public Guid TeamProjectId { get; set; }
        public SourceControlType SourceControlType { get; set; }
    }
}