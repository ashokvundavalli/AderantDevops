using System;
using System.Linq;
using Microsoft.AspNet.WebHooks.Payloads;

namespace Aderant.WebHooks.Model {
    internal class RepositoryInfo {
        private readonly GitRepository resourceRepository;

        public RepositoryInfo(GitRepository resourceRepository) {
            this.resourceRepository = resourceRepository;

            Id = resourceRepository.Id;
            TeamProject = resourceRepository.Project.Name;
            TeamProjectId = resourceRepository.Project.Id;

            ServerUri = ExtractOriginFromResource(resourceRepository);
        }

        private Uri ExtractOriginFromResource(GitRepository resourceRepository) {
            var segments = resourceRepository.Url.Segments.TakeWhile(s => !string.Equals(s, "_apis/", StringComparison.OrdinalIgnoreCase));

            string authority = resourceRepository.Url.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);

            return new Uri(authority + string.Join("", segments));
        }

        public string TeamProjectId { get; private set; }

        public Uri ServerUri { get; private set; }

        public string TeamProject { get; private set; }
        public string Id { get; private set; }
    }
}