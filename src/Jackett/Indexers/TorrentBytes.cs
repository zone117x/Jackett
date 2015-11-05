﻿using CsQuery;
using Jackett.Models;
using Jackett.Services;
using Jackett.Utils;
using Jackett.Utils.Clients;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
    public class TorrentBytes : BaseIndexer, IIndexer
    {
        private string BrowseUrl { get { return SiteLink + "browse.php"; } }
        private string LoginUrl { get { return SiteLink + "takelogin.php"; } }

        new ConfigurationDataBasicLogin configData
        {
            get { return (ConfigurationDataBasicLogin)base.configData; }
            set { base.configData = value; }
        }

        public TorrentBytes(IIndexerManagerService i, IWebClient wc, Logger l, IProtectionService ps)
            : base(name: "TorrentBytes",
                description: "A decade of torrentbytes",
                link: "https://www.torrentbytes.net/",
                caps: TorznabUtil.CreateDefaultTorznabTVCaps(),
                manager: i,
                client: wc,
                logger: l,
                p: ps,
                configData: new ConfigurationDataBasicLogin())
        {

            AddCategoryMapping(41, TorznabCatType.TV);
            AddCategoryMapping(33, TorznabCatType.TVSD);
            AddCategoryMapping(38, TorznabCatType.TVHD);
            AddCategoryMapping(32, TorznabCatType.TVSD);
            AddCategoryMapping(37, TorznabCatType.TVSD);
            AddCategoryMapping(44, TorznabCatType.TVSD);

            AddCategoryMapping(40, TorznabCatType.Movies);
            AddCategoryMapping(19, TorznabCatType.MoviesSD);
            AddCategoryMapping(5, TorznabCatType.MoviesHD);
            AddCategoryMapping(20, TorznabCatType.MoviesSD);
            AddCategoryMapping(28, TorznabCatType.MoviesForeign);
            AddCategoryMapping(45, TorznabCatType.MoviesSD);

            AddCategoryMapping(43, TorznabCatType.Audio);
            AddCategoryMapping(48, TorznabCatType.AudioLossless);
            AddCategoryMapping(6, TorznabCatType.AudioMP3);
            AddCategoryMapping(46, TorznabCatType.Movies);

            AddCategoryMapping(1, TorznabCatType.PC);
            AddCategoryMapping(2, TorznabCatType.PC);
            AddCategoryMapping(23, TorznabCatType.TVAnime);
            AddCategoryMapping(21, TorznabCatType.XXX);
            AddCategoryMapping(9, TorznabCatType.XXXXviD);
            AddCategoryMapping(39, TorznabCatType.XXXx264);
            AddCategoryMapping(29, TorznabCatType.XXXXviD);
            AddCategoryMapping(24, TorznabCatType.XXXImageset);
        }

        public async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            configData.LoadValuesFromJson(configJson);
            var pairs = new Dictionary<string, string> {
                { "username", configData.Username.Value },
                { "password", configData.Password.Value },
                { "returnto", "/" },
                { "login", "Log in!" }
            };

            var loginPage = await RequestStringWithCookies(SiteLink, string.Empty);

            var result = await RequestLoginAndFollowRedirect(LoginUrl, pairs, loginPage.Cookies, true, SiteLink, SiteLink);
            await ConfigureIfOK(result.Cookies, result.Content != null && result.Content.Contains("logout.php"), () =>
            {
                CQ dom = result.Content;
                var messageEl = dom["body > div"].First();
                var errorMessage = messageEl.Text().Trim();
                throw new ExceptionWithConfigData(errorMessage, configData);
            });
            return IndexerConfigurationStatus.RequiresTesting;
        }

        public async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var searchString = query.GetQueryString();
            var searchUrl = BrowseUrl;
            var trackerCats = MapTorznabCapsToTrackers(query);
            var queryCollection = new NameValueCollection();

            // Tracker can only search OR return things in categories
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                queryCollection.Add("search", searchString);
                queryCollection.Add("cat", "0");
                queryCollection.Add("sc", "1");
            }
            else
            {
                foreach (var cat in MapTorznabCapsToTrackers(query))
                {
                    queryCollection.Add("c" + cat, "1");
                }

                queryCollection.Add("incldead", "0");
            }

            searchUrl += "?" + queryCollection.GetQueryString();

            // 15 results per page - really don't want to call the server twice but only 15 results per page is a bit crap!
            await ProcessPage(releases, searchUrl);
            await ProcessPage(releases, searchUrl + "&page=1");
            return releases;
        }

        private async Task ProcessPage(List<ReleaseInfo> releases, string searchUrl)
        {
            var response = await RequestStringWithCookiesAndRetry(searchUrl, null, BrowseUrl);
            var results = response.Content;
            try
            {
                CQ dom = results;

                var rows = dom["#content table:eq(4) tr"];
                // If we return 4 rows the ratio warning banner must be displayed, skip to next table.
                if (rows.Length.Equals(4))
                {
                    rows = dom["#content table:eq(5) tr"];
                }
                foreach (var row in rows.Skip(1))
                {
                    var release = new ReleaseInfo();

                    var link = row.Cq().Find("td:eq(1) a:eq(1)").First();
                    release.Guid = new Uri(SiteLink + link.Attr("href"));
                    release.Comments = release.Guid;
                    release.Title = link.Text().Trim();
                    release.Description = release.Title;

                    // If we search an get no results, we still get a table just with no info.
                    if (string.IsNullOrWhiteSpace(release.Title))
                    {
                        break;
                    }

                    var cat = row.Cq().Find("td:eq(0) a").First().Attr("href").Substring(15);
                    release.Category = MapTrackerCatToNewznab(cat);


                    var qLink = row.Cq().Find("td:eq(1) a").First();
                    release.Link = new Uri(SiteLink + qLink.Attr("href"));

                    var added = row.Cq().Find("td:eq(4)").First().Text().Trim();
                    release.PublishDate = DateTimeUtil.FromTimeAgo(added);

                    var sizeStr = row.Cq().Find("td:eq(6)").First().Text().Trim();
                    release.Size = ReleaseInfo.GetBytes(sizeStr);

                    release.Seeders = ParseUtil.CoerceInt(row.Cq().Find("td:eq(8)").First().Text().Trim());
                    release.Peers = ParseUtil.CoerceInt(row.Cq().Find("td:eq(9)").First().Text().Trim()) + release.Seeders;

                    releases.Add(release);
                }
            }
            catch (Exception ex)
            {
                OnParseError(results, ex);
            }
        }
    }
}
