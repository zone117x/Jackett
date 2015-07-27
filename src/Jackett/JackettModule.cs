using Autofac;
using System;
using System.Linq;
using Autofac.Integration.WebApi;
using Jackett.Indexers;
using Jackett.Utils.Clients;

namespace Jackett
{
    public class JackettModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Just register everything!
            var thisAssembly = typeof(JackettModule).Assembly;
            builder.RegisterAssemblyTypes(thisAssembly).Except<IIndexer>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterApiControllers(thisAssembly).InstancePerRequest();

            // Register the best web client for the platform or exec curl as a safe option
            if (Startup.CurlSafe)
            {
                builder.RegisterType<UnixSafeCurlWebClient>().As<IWebClient>();
            }
            else if(System.Environment.OSVersion.Platform == PlatformID.Unix)
            {
                builder.RegisterType<UnixLibCurlWebClient>().As<IWebClient>();
            }
            else
            {
                builder.RegisterType<WindowsWebClient>().As<IWebClient>();
            }

            // Register indexers
            foreach (var indexer in thisAssembly.GetTypes()
                .Where(p => typeof(IIndexer).IsAssignableFrom(p) && !p.IsInterface)
                .ToArray())
            {
                builder.RegisterType(indexer).Named<IIndexer>(BaseIndexer.GetIndexerID(indexer));
            }
        }
    }
}
