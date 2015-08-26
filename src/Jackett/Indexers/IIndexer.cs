﻿using Jackett.Models;
using Jackett.Models.IndexerConfig;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace Jackett.Indexers
{
    public interface IIndexer
    {
        string SiteLink { get; }

        string DisplayName { get; }
        string DisplayDescription { get; }
        string ID { get; }

        TorznabCapabilities TorznabCaps { get; }

        // Whether this indexer has been configured, verified and saved in the past and has the settings required for functioning
        bool IsConfigured { get; }

        // Retrieved for starting setup for the indexer via web API
        Task<ConfigurationData> GetConfigurationForSetup();

        // Called when web API wants to apply setup configuration via web API, usually this is where login and storing cookie happens
        Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson);

        // Called on startup when initializing indexers from saved configuration
        void LoadFromSavedConfiguration(JToken jsonConfig);

        Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query);

        IEnumerable<ReleaseInfo> FilterResults(TorznabQuery query, IEnumerable<ReleaseInfo> input);

        Task<byte[]> Download(Uri link);

        IEnumerable<ReleaseInfo> CleanLinks(IEnumerable<ReleaseInfo> releases);
        Uri UncleanLink(Uri link);
    }
}
