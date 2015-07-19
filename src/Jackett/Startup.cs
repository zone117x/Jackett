﻿using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Autofac.Integration.WebApi;
using Microsoft.Owin;
using Jackett;
using Microsoft.Owin.StaticFiles;
using Microsoft.Owin.FileSystems;
using Autofac;
using Jackett.Services;

[assembly: OwinStartup(typeof(Startup))]
namespace Jackett
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            var config = new HttpConfiguration();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(Engine.GetContainer());
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "Admin",
                routeTemplate: "admin/{action}",
                defaults: new { controller = "Admin" }
            );

            config.Routes.MapHttpRoute(
                name: "api",
                routeTemplate: "api/{indexerName}",
                defaults: new { controller = "API", action = "Call" }
            );

            config.Routes.MapHttpRoute(
                name: "download",
                routeTemplate: "api/{indexerName}/download/{path}/download.torrent",
                defaults: new { controller = "Download", action = "Download" }
            );

            appBuilder.UseFileServer(new FileServerOptions
            {
                RequestPath = new PathString(string.Empty),
                FileSystem = new PhysicalFileSystem(Engine.ConfigService.GetContentFolder()),
                EnableDirectoryBrowsing = true,
            });

            appBuilder.UseWebApi(config);
        }
    }
}