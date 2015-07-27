﻿using Owin;
using System.Web.Http;
using Autofac.Integration.WebApi;
using Microsoft.Owin;
using Jackett;
using Microsoft.Owin.StaticFiles;
using Microsoft.Owin.FileSystems;
using System.Web.Http.Tracing;
using Jackett.Utils;

[assembly: OwinStartup(typeof(Startup))]
namespace Jackett
{
    public class Startup
    {
        public static bool TracingEnabled
        {
            get;
            set;
        }

        public static bool LogRequests
        {
            get;
            set;
        }

        public static bool CurlSafe
        {
            get;
            set;
        }

        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            var config = new HttpConfiguration();

            appBuilder.Use<WebApiRootRedirectMiddleware>();   

            // Setup tracing if enabled
            if (TracingEnabled)
            {
                config.EnableSystemDiagnosticsTracing();
                config.Services.Replace(typeof(ITraceWriter), new WebAPIToNLogTracer());
            }
            // Add request logging if enabled
            if (LogRequests)
            {
                config.MessageHandlers.Add(new WebAPIRequestLogger());
            }
            config.DependencyResolver = new AutofacWebApiDependencyResolver(Engine.GetContainer());
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "Admin",
                routeTemplate: "admin/{action}",
                defaults: new { controller = "Admin" }
            );

            config.Routes.MapHttpRoute(
                name: "apiDefault",
                routeTemplate: "api/{indexerID}",
                defaults: new { controller = "API", action = "Call" }
            );

            config.Routes.MapHttpRoute(
               name: "api",
               routeTemplate: "api/{indexerID}/api",
               defaults: new { controller = "API", action = "Call" }
           );

            config.Routes.MapHttpRoute(
                name: "download",
                routeTemplate: "api/{indexerID}/download/{path}/download.torrent",
                defaults: new { controller = "Download", action = "Download" }
            );

            appBuilder.UseFileServer(new FileServerOptions
            {
                RequestPath = new PathString(string.Empty),
                FileSystem = new PhysicalFileSystem(Engine.ConfigService.GetContentFolder()),
                EnableDirectoryBrowsing = false,
                
            });

            appBuilder.UseWebApi(config);
        }
    }
}
