using Aderant.WebHooks;
using Aderant.WebHooks.Model;
using Microsoft.AspNet.WebHooks.Payloads;

namespace Aderant.TeamFoundation.Integration.Actions {
    internal class GitRepositoryActionBase<T> {
        protected GitRepositoryActionBase(T payload, GitRepository repository) {
            RepositoryInfo = new RepositoryInfo(repository);
            Payload = payload;
            ServiceFactory = new DefaultServiceFactory();
        }

        public T Payload { get; protected set; }

        public RepositoryInfo RepositoryInfo { get; protected set; }

        protected virtual ITeamFoundationServer GetTfsConnection(RepositoryInfo repositoryInfo) {
            return ServiceFactory.CreateTeamFoundationConnection<ITeamFoundationServer>(repositoryInfo.ServerUri);
        }

        public IServiceFactory ServiceFactory { get; set; }
    }
}