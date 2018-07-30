﻿using System;
using System.Net.Http.Formatting;
using System.Web.Http;
using ArtifactService;
using Microsoft.Owin;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Owin;

[assembly: OwinStartup(typeof(Startup))]

namespace ArtifactService {

    public class Startup {
        public void Configuration(IAppBuilder app) {
            //// Configure Web API for self-host. 
            //HttpConfiguration config = new HttpConfiguration();
            //app.UseWebApi(config);

            //WebApiConfig.Register(config);
            app.Use(
                (context, next) => {
                    context.Response.Headers.Remove("Server");
                    return next.Invoke();
                });
            app.UseStageMarker(PipelineStage.PostAcquireState);

            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();

            config.Formatters.Clear();
            config.Formatters.Add(
                new JsonMediaTypeFormatter {
                    Indent = true,
                    SerializerSettings = new JsonSerializerSettings {
                        Formatting = Formatting.Indented
                    }
                });

            // Web API routes
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
            app.UseWebApi(config);
        }
    }

    class Program {

        static void Main(string[] args) {
            using (WebApp.Start<Startup>("http://localhost:9000")) {
                Console.WriteLine("Press [enter] to quit...");
                Console.ReadLine();
            }
        }
    }
}
