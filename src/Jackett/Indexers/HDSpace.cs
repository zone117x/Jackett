﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jackett.Models;
using Newtonsoft.Json.Linq;
using Jackett.Utils.Clients;
using Jackett.Services;
using NLog;
using Jackett.Utils;
using CsQuery;
using System.Web;
using System.Text.RegularExpressions;
using System.Globalization;
using Jackett.Models.IndexerConfig;

namespace Jackett.Indexers
{
    public class HDSpace : BaseIndexer, IIndexer
    {
        private string LoginUrl { get { return SiteLink + "index.php?page=login"; } }
        private string SearchUrl { get { return SiteLink + "index.php?page=torrents&active=0&options=0&category=21%3B22&search={0}"; } }

        new ConfigurationDataBasicLogin configData
        {
            get { return (ConfigurationDataBasicLogin)base.configData; }
            set { base.configData = value; }
        }

        public HDSpace(IIndexerManagerService i, IWebClient wc, Logger l, IProtectionService ps)
            : base(name: "HD-Space",
                description: "Sharing The Universe",
                link: "https://hd-space.org/",
                caps: TorznabUtil.CreateDefaultTorznabTVCaps(),
                manager: i,
                client: wc,
                logger: l,
                p: ps,
                configData: new ConfigurationDataBasicLogin())
        {
        }

        public async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            configData.LoadValuesFromJson(configJson);

            var loginPage = await RequestStringWithCookies(LoginUrl, string.Empty);

            var pairs = new Dictionary<string, string> {
                { "uid", configData.Username.Value },
                { "pwd", configData.Password.Value }
            };

            // Send Post
            var response = await RequestLoginAndFollowRedirect(LoginUrl, pairs, loginPage.Cookies, true, null, LoginUrl);

            await ConfigureIfOK(response.Cookies, response.Content != null && response.Content.Contains("logout.php"), () =>
            {
                var errorStr = "You have {0} remaining login attempts";
                var remainingAttemptSpan = new Regex(string.Format(errorStr, "(.*?)")).Match(loginPage.Content).Groups[1].ToString();
                var attempts = Regex.Replace(remainingAttemptSpan, "<.*?>", String.Empty);
                var errorMessage = string.Format(errorStr, attempts);
                throw new ExceptionWithConfigData(errorMessage, configData);
            });
            return IndexerConfigurationStatus.RequiresTesting;
        }

        public async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var episodeSearchUrl = string.Format(SearchUrl, HttpUtility.UrlEncode(query.GetQueryString()));
            var response = await RequestStringWithCookiesAndRetry(episodeSearchUrl);
            var results = response.Content;

            try
            {
                CQ dom = results;
                var rows = dom["table.lista > tbody > tr"];
                foreach (var row in rows)
                {
                    // this tracker has horrible markup, find the result rows by looking for the style tag before each one
                    var prev = row.PreviousElementSibling;
                    if (prev == null || prev.NodeName.ToLowerInvariant() != "style") continue;

                    CQ qRow = row.Cq();
                    var release = new ReleaseInfo();

                    release.MinimumRatio = 1;
                    release.MinimumSeedTime = 172800;

                    var qLink = row.ChildElements.ElementAt(1).FirstElementChild.Cq();
                    release.Title = qLink.Text().Trim();
                    release.Comments = new Uri(SiteLink + qLink.Attr("href"));
                    release.Guid = release.Comments;

                    var qDownload = row.ChildElements.ElementAt(3).FirstElementChild.Cq();
                    release.Link = new Uri(SiteLink + qDownload.Attr("href"));

                    //"July 11, 2015, 13:34:09", "Today at 20:04:23"
                    var dateStr = row.ChildElements.ElementAt(4).Cq().Text().Trim();
                    if (dateStr.StartsWith("Today"))
                        release.PublishDate = DateTime.Today + TimeSpan.ParseExact(dateStr.Replace("Today at ", ""), "hh\\:mm\\:ss", CultureInfo.InvariantCulture);
                    else if (dateStr.StartsWith("Yesterday"))
                        release.PublishDate = DateTime.Today - TimeSpan.FromDays(1) + TimeSpan.ParseExact(dateStr.Replace("Yesterday at ", ""), "hh\\:mm\\:ss", CultureInfo.InvariantCulture);
                    else
                        release.PublishDate = DateTime.SpecifyKind(DateTime.ParseExact(dateStr, "MMMM dd, yyyy, HH:mm:ss", CultureInfo.InvariantCulture), DateTimeKind.Local);

                    var sizeStr = row.ChildElements.ElementAt(5).Cq().Text();
                    release.Size = ReleaseInfo.GetBytes(sizeStr);

                    release.Seeders = ParseUtil.CoerceInt(row.ChildElements.ElementAt(7).Cq().Text());
                    release.Peers = ParseUtil.CoerceInt(row.ChildElements.ElementAt(8).Cq().Text()) + release.Seeders;

                    releases.Add(release);
                }
            }
            catch (Exception ex)
            {
                OnParseError(results, ex);
            }
            return releases;
        }
    }
}
