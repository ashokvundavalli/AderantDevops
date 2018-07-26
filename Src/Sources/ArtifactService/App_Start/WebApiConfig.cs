using System.Web.Http;

namespace ArtifactService {
    public static class WebApiConfig {
        public static void Register(HttpConfiguration config) {
            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
