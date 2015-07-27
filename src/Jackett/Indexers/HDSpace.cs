﻿using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Jackett.Indexers
{
    public class HDSpace : BaseIndexer, IIndexer
    {

        private readonly string LoginUrl = "";
        private readonly string SearchUrl = "";
        private string cookieHeader = "";

        private IWebClient webclient;

        public HDSpace(IIndexerManagerService i, IWebClient wc, Logger l)
            : base(name: "HD-Space",
                description: "Sharing The Universe",
                link: new Uri("https://hd-space.org"),
                caps: TorznabCapsUtil.CreateDefaultTorznabTVCaps(),
                manager: i,
                logger: l)
        {
            LoginUrl = SiteLink + "index.php?page=login";
            SearchUrl = SiteLink + "index.php?page=torrents&active=0&options=0&category=21%3B22&search={0}";
            webclient = wc;
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

            var loginPage = await webclient.GetString(new WebRequest()
            {
                Url = LoginUrl,
                Type = RequestType.GET
            });

            var pairs = new Dictionary<string, string> {
                { "uid", config.Username.Value },
                { "pwd", config.Password.Value }
            };

            // Send Post
            var loginPost = await webclient.GetString(new WebRequest()
            {
                Url = LoginUrl,
                PostData = pairs,
                Referer = LoginUrl,
                Type = RequestType.POST,
                Cookies = loginPage.Cookies
            });

            if (loginPost.Status == System.Net.HttpStatusCode.OK)
            {
                var errorStr = "You have {0} remaining login attempts";
                var remainingAttemptSpan = new Regex(string.Format(errorStr, "(.*?)")).Match(loginPage.Content).Groups[1].ToString();
                var attempts = Regex.Replace(remainingAttemptSpan, "<.*?>", String.Empty);
                var errorMessage = string.Format(errorStr, attempts);
                throw new ExceptionWithConfigData(errorMessage, (ConfigurationData)config);
            }

            // Get result from redirect
            var loginResult = await webclient.GetString(new WebRequest()
            {
                Url = SiteLink + loginPost.RedirectingTo,
                Type = RequestType.GET,
                Cookies = loginPost.Cookies
            });

            if (!loginResult.Content.Contains("logout.php"))
            {
                throw new ExceptionWithConfigData("Login failed", (ConfigurationData)config);
            }
            else
            {
                cookieHeader = loginPost.Cookies;
                var configSaveData = new JObject();
                configSaveData["cookies"] = cookieHeader;
                SaveConfig(configSaveData);
                IsConfigured = true;
            }

        }

        public void LoadFromSavedConfiguration(JToken jsonConfig)
        {
            cookieHeader = (string)jsonConfig["cookies"];
            IsConfigured = true;
        }

        public async Task<byte[]> Download(Uri link)
        {
            var response = await webclient.GetBytes(new WebRequest()
            {
                Url = link.ToString(),
                Cookies = cookieHeader
            });
            return response.Content;
        }

        public async Task<ReleaseInfo[]> PerformQuery(TorznabQuery query)
        {
            List<ReleaseInfo> releases = new List<ReleaseInfo>();

            var searchString = query.SanitizedSearchTerm + " " + query.GetEpisodeSearchString();
            var episodeSearchUrl = string.Format(SearchUrl, HttpUtility.UrlEncode(searchString));

            var response = await webclient.GetString(new WebRequest()
            {
                Url = episodeSearchUrl,
                Referer = SiteLink.ToString(),
                Cookies = cookieHeader
            });
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
            return releases.ToArray();
        }
    }
}
