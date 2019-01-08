using System.Web.Http;

namespace Aderant.TeamFoundation.Integration {
    internal static class WebApiConfig {
        public static void Register(HttpConfiguration config) {
            // Web API configuration and services
            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
                );

            // Load receivers
            // api/webhooks/incoming/vsts/{id}?code={code}
            config.InitializeReceiveVstsWebHooks();
        }
    }
}