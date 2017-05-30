using System;
using Aderant.TeamFoundation.Integration;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Aderant.WebHooks {
    internal class DefaultServiceFactory : IServiceFactory {
        public T CreateTeamFoundationConnection<T>(Uri serverUri) where T : ITeamFoundationServer {
            return (T)(object)new TeamFoundationServer(new VssConnection(serverUri, new VssCredentials()));
        }
    }
}