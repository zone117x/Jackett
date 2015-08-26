﻿using CsQuery;
using Jackett.Models;
using Jackett.Services;
using Jackett.Utils;
using Jackett.Utils.Clients;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Jackett.Models.IndexerConfig;
using System.Collections.Specialized;

namespace Jackett.Indexers
{
    // To comply with the rules for this tracker, only the acronym is used and no publicly displayed URLs to the site. 

    public class BB : BaseIndexer, IIndexer
    {
        private string BaseUrl { get { return StringUtil.FromBase64("aHR0cHM6Ly9iYWNvbmJpdHMub3JnLw=="); } }
        private Uri BaseUri { get { return new Uri(BaseUrl); } }
        private string LoginUrl { get { return BaseUri + "login.php"; } }
        private string SearchUrl { get { return BaseUri + "torrents.php?searchtags=&tags_type=0&order_by=s3&order_way=desc&disablegrouping=1&"; } }

        new ConfigurationDataBasicLogin configData
        {
            get { return (ConfigurationDataBasicLogin)base.configData; }
            set { base.configData = value; }
        }

        public BB(IIndexerManagerService i, Logger l, IWebClient w, IProtectionService ps)
            : base(name: "bB",
                description: "bB",
                link: "http://www.reddit.com/r/baconbits/",
                caps: new TorznabCapabilities(),
                manager: i,
                client: w,
                logger: l,
                p: ps,
                configData: new ConfigurationDataBasicLogin())
        {
            AddCategoryMapping(1, TorznabCatType.Audio);
            AddCategoryMapping(2, TorznabCatType.PC);
            AddCategoryMapping(3, TorznabCatType.BooksEbook);
            AddCategoryMapping(4, TorznabCatType.AudioAudiobook);
            AddCategoryMapping(6, TorznabCatType.BooksComics);
            AddCategoryMapping(8, TorznabCatType.TVAnime);
            AddCategoryMapping(9, TorznabCatType.Movies);
            AddCategoryMapping(10, TorznabCatType.TVHD);
            AddCategoryMapping(10, TorznabCatType.TVSD);
            AddCategoryMapping(10, TorznabCatType.TV);
            AddCategoryMapping(11, TorznabCatType.PCGames);
        }

        public async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            configData.LoadValuesFromJson(configJson);
            var pairs = new Dictionary<string, string> {
                { "username", configData.Username.Value },
                { "password", configData.Password.Value },
                { "keeplogged", "1" },
                { "login", "Log In!" }
            };

            var response = await RequestLoginAndFollowRedirect(LoginUrl, pairs, null, true, null, SiteLink);
            await ConfigureIfOK(response.Cookies, response.Content != null && response.Content.Contains("logout.php"), () =>
            {
                CQ dom = response.Content;
                var messageEl = dom["#loginform"];
                var messages = new List<string>();
                for (var i = 0; i < 13; i++)
                {
                    var child = messageEl[0].ChildNodes[i];
                    messages.Add(child.Cq().Text().Trim());
                }
                var message = string.Join(" ", messages);
                throw new ExceptionWithConfigData(message, configData);
            });

            return IndexerConfigurationStatus.RequiresTesting;
        }

        public async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            List<ReleaseInfo> releases = new List<ReleaseInfo>();

            var searchString = query.GetQueryString();
            var searchUrl = SearchUrl;
            var queryCollection = new NameValueCollection();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                queryCollection.Add("searchstr", searchString);
            }

            foreach (var cat in MapTorznabCapsToTrackers(query))
            {
                queryCollection.Add("filter_cat[" + cat + "]", "1");
            }

            searchUrl += queryCollection.GetQueryString();

            var results = await RequestStringWithCookiesAndRetry(searchUrl);

            try
            {
                CQ dom = results.Content;
                var rows = dom["#torrent_table > tbody > tr.torrent"];
                foreach (var row in rows)
                {
                    CQ qRow = row.Cq();
                    var release = new ReleaseInfo();

                    release.MinimumRatio = 1;
                    release.MinimumSeedTime = 172800;

                    var catStr = row.ChildElements.ElementAt(0).FirstElementChild.GetAttribute("href").Split(new char[] { '[', ']' })[1];
                    release.Category = MapTrackerCatToNewznab(catStr);

                    var qLink = row.ChildElements.ElementAt(1).Cq().Children("a")[0].Cq();
                    var linkStr = qLink.Attr("href");
                    release.Title = qLink.Text();
                    release.Comments = new Uri(BaseUrl + "/" + linkStr);
                    release.Guid = release.Comments;

                    var qDownload = row.ChildElements.ElementAt(1).Cq().Find("a[title='Download']")[0].Cq();
                    release.Link = new Uri(BaseUrl + "/" + qDownload.Attr("href"));

                    var dateStr = row.ChildElements.ElementAt(3).Cq().Text().Trim().Replace(" and", "");
                    release.PublishDate = DateTimeUtil.FromTimeAgo(dateStr);

                    var sizeStr = row.ChildElements.ElementAt(4).Cq().Text();
                    release.Size = ReleaseInfo.GetBytes(sizeStr);

                    release.Seeders = ParseUtil.CoerceInt(row.ChildElements.ElementAt(7).Cq().Text().Trim());
                    release.Peers = ParseUtil.CoerceInt(row.ChildElements.ElementAt(8).Cq().Text().Trim()) + release.Seeders;

                    releases.Add(release);
                }
            }
            catch (Exception ex)
            {
                OnParseError(results.Content, ex);
            }
            return releases;
        }
    }
}
