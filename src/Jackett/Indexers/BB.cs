﻿using CsQuery;
using Jackett.Models;
using Jackett.Services;
using Jackett.Utils;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace Jackett.Indexers
{
    // To comply with the rules for this tracker, only the acronym is used and no publicly displayed URLs to the site. 

    public class BB : BaseIndexer, IIndexer
    {
        private readonly string BaseUrl = "";
        private readonly string LoginUrl = "";
        private readonly string SearchUrl = "";

        CookieContainer cookies;
        HttpClientHandler handler;
        HttpClient client;

        public BB(IIndexerManagerService i, Logger l)
            : base(name: "bB",
                description: "bB",
                link: new Uri("http://www.reddit.com/r/baconbits"),
                caps: TorznabCapsUtil.CreateDefaultTorznabTVCaps(),
                manager: i,
                logger: l)
        {

            BaseUrl = StringUtil.FromBase64("aHR0cHM6Ly9iYWNvbmJpdHMub3Jn");
            LoginUrl = BaseUrl + "/login.php";
            SearchUrl = BaseUrl + "/torrents.php?searchstr={0}&searchtags=&tags_type=0&order_by=s3&order_way=desc&disablegrouping=1&filter_cat%5B10%5D=1";
            cookies = new CookieContainer();
            handler = new HttpClientHandler
            {
                CookieContainer = cookies,
                AllowAutoRedirect = true,
                UseCookies = true,
            };
            client = new HttpClient(handler);
        }

        public Task<ConfigurationData> GetConfigurationForSetup()
        {
            var config = new ConfigurationDataBasicLogin();
            return Task.FromResult<ConfigurationData>(config);
        }

        public async Task ApplyConfiguration(JToken configJson)
        {
            var config = new ConfigurationDataBasicLogin();
            config.LoadValuesFromJson(configJson);

            var pairs = new Dictionary<string, string> {
				{ "username", config.Username.Value },
				{ "password", config.Password.Value },
                { "keeplogged", "1" },
                { "login", "Log In!" }
			};

            var content = new FormUrlEncodedContent(pairs);
            var response = await client.PostAsync(LoginUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!responseContent.Contains("logout.php"))
            {
                CQ dom = responseContent;
                var messageEl = dom["#loginform"];
                var messages = new List<string>();
                for (var i = 0; i < 13; i++)
                {
                    var child = messageEl[0].ChildNodes[i];
                    messages.Add(child.Cq().Text().Trim());
                }
                var message = string.Join(" ", messages);
                throw new ExceptionWithConfigData(message, (ConfigurationData)config);
            }
            else
            {

                var configSaveData = new JObject();
                cookies.DumpToJson(BaseUrl, configSaveData);
                SaveConfig(configSaveData);
                IsConfigured = true;
            }

        }

        public void LoadFromSavedConfiguration(JToken jsonConfig)
        {
            cookies.FillFromJson(new Uri(BaseUrl), jsonConfig, logger);
            IsConfigured = true;
        }

        public async Task<ReleaseInfo[]> PerformQuery(TorznabQuery query)
        {
            List<ReleaseInfo> releases = new List<ReleaseInfo>();

            var searchString = query.SanitizedSearchTerm + " " + query.GetEpisodeSearchString();
            var episodeSearchUrl = string.Format(SearchUrl, HttpUtility.UrlEncode(searchString));
            var results = await client.GetStringAsync(episodeSearchUrl);
            try
            {
                CQ dom = results;
                var rows = dom["#torrent_table > tbody > tr.torrent"];
                foreach (var row in rows)
                {
                    CQ qRow = row.Cq();
                    var release = new ReleaseInfo();

                    release.MinimumRatio = 1;
                    release.MinimumSeedTime = 172800;

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
                OnParseError(results, ex);
            }
            return releases.ToArray();
        }

        public Task<byte[]> Download(Uri link)
        {
            return client.GetByteArrayAsync(link);
        }

    }
}
