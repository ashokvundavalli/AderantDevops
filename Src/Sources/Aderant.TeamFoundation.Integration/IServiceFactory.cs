using System;
using Aderant.WebHooks;

namespace Aderant.TeamFoundation.Integration {
    internal interface IServiceFactory {
        T CreateTeamFoundationConnection<T>(Uri serverUri) where T : ITeamFoundationServer;
    }
}