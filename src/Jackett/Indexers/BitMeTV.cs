﻿using CsQuery;
using Jackett.Models;
using Jackett.Models.IndexerConfig;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Jackett.Indexers
{
    public class BitMeTV : BaseIndexer, IIndexer
    {
        private string LoginUrl { get { return SiteLink + "login.php"; } }
        private string LoginPost { get { return SiteLink + "takelogin.php"; } }
        private string CaptchaUrl { get { return SiteLink + "visual.php"; } }
        private string SearchUrl { get { return SiteLink + "browse.php"; } }

        new ConfigurationDataCaptchaLogin configData
        {
            get { return (ConfigurationDataCaptchaLogin)base.configData; }
            set { base.configData = value; }
        }

        public BitMeTV(IIndexerManagerService i, Logger l, IWebClient c, IProtectionService ps)
            : base(name: "BitMeTV",
                description: "TV Episode specialty tracker",
                link: "http://www.bitmetv.org/",
                caps: TorznabUtil.CreateDefaultTorznabTVCaps(),
                manager: i,
                client: c,
                logger: l,
                p: ps,
                configData: new ConfigurationDataCaptchaLogin())
        {
        }

        public override async Task<ConfigurationData> GetConfigurationForSetup()
        {
            var response = await webclient.GetString(new Utils.Clients.WebRequest()
            {
                Url = LoginUrl
            });
            CookieHeader = response.Cookies;
            var captchaImage = await RequestBytesWithCookies(CaptchaUrl);
            configData.CaptchaImage.Value = captchaImage.Content;
            configData.CaptchaCookie.Value = captchaImage.Cookies;
            return configData;
        }

        public async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            configData.LoadValuesFromJson(configJson);

            var pairs = new Dictionary<string, string> {
                { "username", configData.Username.Value },
                { "password", configData.Password.Value },
                { "secimage", configData.CaptchaText.Value }
            };

            var response = await RequestLoginAndFollowRedirect(LoginPost, pairs, configData.CaptchaCookie.Value, true);
            await ConfigureIfOK(response.Cookies, response.Content.Contains("/logout.php"), async () =>
            {
                CQ dom = response.Content;
                var messageEl = dom["table tr > td.embedded > h2"].Last();
                var errorMessage = messageEl.Text();
                var captchaImage = await RequestBytesWithCookies(CaptchaUrl);
                configData.CaptchaImage.Value = captchaImage.Content;
                configData.CaptchaText.Value = "";
                configData.CaptchaCookie.Value = captchaImage.Cookies;
                throw new ExceptionWithConfigData(errorMessage, configData);
            });
            return IndexerConfigurationStatus.RequiresTesting;
        }

        public async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var episodeSearchUrl = string.Format("{0}?search={1}&cat=0", SearchUrl, HttpUtility.UrlEncode(query.GetQueryString()));
            var results = await RequestStringWithCookiesAndRetry(episodeSearchUrl);
            try
            {
                CQ dom = results.Content;

                var table = dom["tbody > tr > .latest"].Parent().Parent();

                foreach (var row in table.Children().Skip(1))
                {
                    var release = new ReleaseInfo();

                    CQ qDetailsCol = row.ChildElements.ElementAt(1).Cq();
                    CQ qLink = qDetailsCol.Children("a").First();

                    release.MinimumRatio = 1;
                    release.MinimumSeedTime = 172800;
                    release.Comments = new Uri(SiteLink + "/" + qLink.Attr("href"));
                    release.Guid = release.Comments;
                    release.Title = qLink.Attr("title");
                    release.Description = release.Title;

                    //"Tuesday, June 11th 2013 at 03:52:53 AM" to...
                    //"Tuesday June 11 2013 03:52:53 AM"
                    var timestamp = qDetailsCol.Children("font").Text().Trim() + " ";
                    var timeParts = new List<string>(timestamp.Replace(" at", "").Replace(",", "").Split(' '));
                    timeParts[2] = Regex.Replace(timeParts[2], "[^0-9.]", "");
                    var formattedTimeString = string.Join(" ", timeParts.ToArray()).Trim();
                    var date = DateTime.ParseExact(formattedTimeString, "dddd MMMM d yyyy hh:mm:ss tt", CultureInfo.InvariantCulture);
                    release.PublishDate = DateTime.SpecifyKind(date, DateTimeKind.Utc).ToLocalTime();

                    release.Link = new Uri(SiteLink + "/" + row.ChildElements.ElementAt(2).Cq().Children("a.index").Attr("href"));

                    var sizeStr = row.ChildElements.ElementAt(6).Cq().Text();
                    release.Size = ReleaseInfo.GetBytes(sizeStr);

                    release.Seeders = ParseUtil.CoerceInt(row.ChildElements.ElementAt(8).Cq().Text());
                    release.Peers = ParseUtil.CoerceInt(row.ChildElements.ElementAt(9).Cq().Text()) + release.Seeders;

                    //if (!release.Title.ToLower().Contains(title.ToLower()))
                    //    continue;

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
