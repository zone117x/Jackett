using Jackett.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Jackett.Indexers
{
    public interface IIndexer
    {
        string DisplayName { get; }
        string DisplayDescription { get; }
        string ID { get; }

        Uri SiteLink { get; }

        TorznabCapabilities TorznabCaps { get; }

        // Whether this indexer has been configured, verified and saved in the past and has the settings required for functioning
        bool IsConfigured { get; }

        // Retrieved for starting setup for the indexer via web API
        Task<ConfigurationData> GetConfigurationForSetup();

        // Called when web API wants to apply setup configuration via web API, usually this is where login and storing cookie happens
        Task ApplyConfiguration(JToken configJson);

        // Called on startup when initializing indexers from saved configuration
        void LoadFromSavedConfiguration(JToken jsonConfig);

        Task<ReleaseInfo[]> PerformQuery(TorznabQuery query);

        Task<byte[]> Download(Uri link);
    }
}
