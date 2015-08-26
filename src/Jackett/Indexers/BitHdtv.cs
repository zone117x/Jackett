﻿using CsQuery;
using Jackett.Models;
using Jackett.Services;
using Jackett.Utils;
using Jackett.Utils.Clients;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    public class BitHdtv : BaseIndexer, IIndexer
    {
        private string LoginUrl { get { return SiteLink + "takelogin.php"; } }
        private string SearchUrl { get { return SiteLink + "torrents.php?"; } }
        private string DownloadUrl { get { return SiteLink + "download.php?/{0}/dl.torrent"; } }

        new ConfigurationDataBasicLogin configData
        {
            get { return (ConfigurationDataBasicLogin)base.configData; }
            set { base.configData = value; }
        }

        public BitHdtv(IIndexerManagerService i, Logger l, IWebClient w, IProtectionService ps)
            : base(name: "BIT-HDTV",
                description: "Home of high definition invites",
                link: "https://www.bit-hdtv.com/",
                caps: new TorznabCapabilities(),
                manager: i,
                client: w,
                logger: l,
                p: ps,
                configData: new ConfigurationDataBasicLogin())
        {
            AddCategoryMapping(1, TorznabCatType.TVAnime); // Anime
            AddCategoryMapping(2, TorznabCatType.MoviesBluRay); // Blu-ray
            AddCategoryMapping(4, TorznabCatType.TVDocumentary); // Documentaries
            AddCategoryMapping(6, TorznabCatType.AudioLossless); // HQ Audio
            AddCategoryMapping(7, TorznabCatType.Movies); // Movies
            AddCategoryMapping(8, TorznabCatType.AudioVideo); // Music Videos
            AddCategoryMapping(5, TorznabCatType.TVSport); // Sports
            AddCategoryMapping(10, TorznabCatType.TV); // TV
            AddCategoryMapping(12, TorznabCatType.TV); // TV/Seasonpack
            AddCategoryMapping(11, TorznabCatType.XXX); // XXX
        }

        public async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            configData.LoadValuesFromJson(configJson);

            var pairs = new Dictionary<string, string> {
                { "username", configData.Username.Value },
                { "password", configData.Password.Value }
            };

            var response = await RequestLoginAndFollowRedirect(LoginUrl, pairs, null, true, null, SiteLink);
            await ConfigureIfOK(response.Cookies, response.Content != null && response.Content.Contains("logout.php"), () =>
            {
                CQ dom = response.Content;
                var messageEl = dom["table.detail td.text"].Last();
                messageEl.Children("a").Remove();
                messageEl.Children("style").Remove();
                var errorMessage = messageEl.Text().Trim();
                throw new ExceptionWithConfigData(errorMessage, configData);
            });
            return IndexerConfigurationStatus.RequiresTesting;
        }

        public async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var searchString = query.GetQueryString();
            var queryCollection = new NameValueCollection();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                queryCollection.Add("search", searchString);
            }

            var searchUrl = SearchUrl + queryCollection.GetQueryString();

            var trackerCats = MapTorznabCapsToTrackers(query, mapChildrenCatsToParent: true);

            var results = await RequestStringWithCookiesAndRetry(searchUrl);
            try
            {
                CQ dom = results.Content;
                dom["#needseed"].Remove();
                var rows = dom["table[width='750'] > tbody"].Children();
                foreach (var row in rows.Skip(1))
                {

                    var release = new ReleaseInfo();

                    var qRow = row.Cq();
                    var qLink = qRow.Children().ElementAt(2).Cq().Children("a").First();

                    release.MinimumRatio = 1;
                    release.MinimumSeedTime = 172800;
                    release.Title = qLink.Attr("title");
                    release.Description = release.Title;
                    release.Guid = new Uri(SiteLink + qLink.Attr("href").TrimStart('/'));
                    release.Comments = release.Guid;
                    release.Link = new Uri(string.Format(DownloadUrl, qLink.Attr("href").Split('=')[1]));

                    var catUrl = qRow.Children().ElementAt(1).FirstElementChild.Cq().Attr("href");
                    var catNum = catUrl.Split(new char[] { '=', '&' })[1];
                    release.Category = MapTrackerCatToNewznab(catNum);

                    // This tracker cannot search multiple cats at a time, so search all cats then filter out results from different cats
                    if (trackerCats.Count > 0 && !trackerCats.Contains(catNum))
                        continue;

                    var dateString = qRow.Children().ElementAt(5).Cq().Text().Trim();
                    var pubDate = DateTime.ParseExact(dateString, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    release.PublishDate = DateTime.SpecifyKind(pubDate, DateTimeKind.Local);

                    var sizeStr = qRow.Children().ElementAt(6).Cq().Text();
                    release.Size = ReleaseInfo.GetBytes(sizeStr);

                    release.Seeders = ParseUtil.CoerceInt(qRow.Children().ElementAt(8).Cq().Text().Trim());
                    release.Peers = ParseUtil.CoerceInt(qRow.Children().ElementAt(9).Cq().Text().Trim()) + release.Seeders;

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
