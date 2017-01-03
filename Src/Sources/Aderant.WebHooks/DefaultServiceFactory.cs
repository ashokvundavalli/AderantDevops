using System;
using Aderant.WebHooks.Actions;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;

namespace Aderant.WebHooks {
    internal class DefaultServiceFactory : IServiceFactory {
        public T CreateTeamFoundationConnection<T>(Uri serverUri) where T : ITeamFoundationServer {
            return (T)(object)new TeamFoundationServer(new VssConnection(serverUri, new VssCredentials()));
        }
    }
}