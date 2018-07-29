using System;
using System.Net.Http.Formatting;
using System.Web.Http;
using Microsoft.Owin;

using Owin;

[assembly: OwinStartup(typeof(ArtifactService.Startup))]

namespace ArtifactService {
    public class Startup {
        public void Configuration(IAppBuilder app) {

            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.Formatters.Clear();
            config.Formatters.Add(new JsonMediaTypeFormatter());

            app.UseWebApi(config);
        }
    }

    class Program {

        static void Main(string[] args) {
            using (Microsoft.Owin.Hosting.WebApp.Start<Startup>("http://localhost:9000")) {
                Console.WriteLine("Press [enter] to quit...");
                Console.ReadLine();
            }
        }
    }
}
