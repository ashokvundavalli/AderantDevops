using System;

namespace Aderant.WebHooks {
    internal interface IServiceFactory {
        T CreateTeamFoundationConnection<T>(Uri serverUri) where T : ITeamFoundationServer;
    }
}