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

namespace Jackett.Indexers
{
    public class TorrentLeech : BaseIndexer, IIndexer
    {
        private string LoginUrl { get { return SiteLink + "user/account/login/"; } }
        private string SearchUrl { get { return SiteLink + "torrents/browse/index/"; } }

        new ConfigurationDataBasicLogin configData
        {
            get { return (ConfigurationDataBasicLogin)base.configData; }
            set { base.configData = value; }
        }

        public TorrentLeech(IIndexerManagerService i, Logger l, IWebClient wc, IProtectionService ps)
            : base(name: "TorrentLeech",
                description: "This is what happens when you seed",
                link: "http://www.torrentleech.org/",
                caps: TorznabUtil.CreateDefaultTorznabTVCaps(),
                manager: i,
                client: wc,
                logger: l,
                p: ps,
                downloadBase: "http://www.torrentleech.org/download/",
                configData: new ConfigurationDataBasicLogin())
        {

            AddCategoryMapping(8, TorznabCatType.MoviesSD); // cam
            AddCategoryMapping(9, TorznabCatType.MoviesSD); //ts
            AddCategoryMapping(10, TorznabCatType.MoviesSD); // Sceener
            AddCategoryMapping(11, TorznabCatType.MoviesSD);
            AddCategoryMapping(12, TorznabCatType.MoviesSD);
            AddCategoryMapping(13, TorznabCatType.MoviesHD);
            AddCategoryMapping(14, TorznabCatType.MoviesHD);
            AddCategoryMapping(15, TorznabCatType.Movies); // Boxsets
            AddCategoryMapping(29, TorznabCatType.TVDocumentary);

            AddCategoryMapping(26, TorznabCatType.TVSD);
            AddCategoryMapping(27, TorznabCatType.TV); // Boxsets
            AddCategoryMapping(32, TorznabCatType.TVHD);

            AddCategoryMapping(17, TorznabCatType.PCGames);
            AddCategoryMapping(18, TorznabCatType.ConsoleXbox);
            AddCategoryMapping(19, TorznabCatType.ConsoleXbox360);
            // 20 PS2
            AddCategoryMapping(21, TorznabCatType.ConsolePS3);
            AddCategoryMapping(22, TorznabCatType.ConsolePSP);
            AddCategoryMapping(28, TorznabCatType.ConsoleWii);
            AddCategoryMapping(30, TorznabCatType.ConsoleNDS);

            AddCategoryMapping(16, TorznabCatType.AudioVideo);
            AddCategoryMapping(31, TorznabCatType.Audio);

            AddCategoryMapping(34, TorznabCatType.TVAnime);
            AddCategoryMapping(35, TorznabCatType.TV); // Cartoons

            AddCategoryMapping(5, TorznabCatType.Books);

            AddCategoryMapping(23, TorznabCatType.PCISO);
            AddCategoryMapping(24, TorznabCatType.PCMac);
            AddCategoryMapping(25, TorznabCatType.PCPhoneOther);
            AddCategoryMapping(33, TorznabCatType.PC0day);
        }

        public async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            configData.LoadValuesFromJson(configJson);
            var pairs = new Dictionary<string, string> {
                { "username", configData.Username.Value },
                { "password", configData.Password.Value },
                { "remember_me", "on" },
                { "login", "submit" }
            };

            var result = await RequestLoginAndFollowRedirect(LoginUrl, pairs, null, true, null, LoginUrl);
            await ConfigureIfOK(result.Cookies, result.Content != null && result.Content.Contains("/user/account/logout"), () =>
            {
                CQ dom = result.Content;
                var messageEl = dom[".ui-state-error"].Last();
                var errorMessage = messageEl.Text().Trim();
                throw new ExceptionWithConfigData(errorMessage, configData);
            });
            return IndexerConfigurationStatus.RequiresTesting;
        }

        public async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var searchString = query.GetQueryString();
            var searchUrl = SearchUrl;

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchUrl += "query/" + HttpUtility.UrlEncode(searchString) + "/";
            }
            string.Format(SearchUrl, HttpUtility.UrlEncode(searchString));

            var cats = MapTorznabCapsToTrackers(query);
            if (cats.Count > 0)
            {
                searchUrl += "categories/";
                foreach (var cat in cats)
                {
                    if (!searchUrl.EndsWith("/"))
                        searchUrl += ",";
                    searchUrl += cat;
                }
            }

            var results = await RequestStringWithCookiesAndRetry(searchUrl);
            try
            {
                CQ dom = results.Content;

                CQ qRows = dom["#torrenttable > tbody > tr"];

                foreach (var row in qRows)
                {
                    var release = new ReleaseInfo();

                    var qRow = row.Cq();

                    var debug = qRow.Html();

                    release.MinimumRatio = 1;
                    release.MinimumSeedTime = 172800;

                    CQ qLink = qRow.Find(".title > a").First();
                    release.Guid = new Uri(SiteLink + qLink.Attr("href").Substring(1));
                    release.Comments = release.Guid;
                    release.Title = qLink.Text();
                    release.Description = release.Title;

                    release.Link = new Uri(SiteLink + qRow.Find(".quickdownload > a").Attr("href").Substring(1));

                    var dateString = qRow.Find(".name")[0].InnerText.Trim().Replace(" ", string.Empty).Replace("Addedinon", string.Empty);
                    //"2015-04-25 23:38:12"
                    //"yyyy-MMM-dd hh:mm:ss"
                    release.PublishDate = DateTime.ParseExact(dateString, "yyyy-MM-ddHH:mm:ss", CultureInfo.InvariantCulture);

                    var sizeStr = qRow.Children().ElementAt(4).InnerText;
                    release.Size = ReleaseInfo.GetBytes(sizeStr);

                    release.Seeders = ParseUtil.CoerceInt(qRow.Find(".seeders").Text());
                    release.Peers = release.Seeders + ParseUtil.CoerceInt(qRow.Find(".leechers").Text());

                    var category = qRow.Find(".category a").Attr("href").Replace("/torrents/browse/index/categories/", string.Empty);
                    release.Category = MapTrackerCatToNewznab(category);

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
