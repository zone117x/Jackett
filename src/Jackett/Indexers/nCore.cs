using CsQuery;
using Jackett.Models;
using Jackett.Services;
using Jackett.Utils;
using Newtonsoft.Json;
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

namespace Jackett.Indexers
{
    public class nCore : BaseIndexer, IIndexer
    {
        private readonly string SearchUrl = "http://ncore.cc/torrents.php";
        private static string LoginUrl = "https://ncore.cc/login.php";
        private readonly string LoggedInUrl = "http://ncore.cc/index.php";
        private const int MAXPAGES = 3;

        private readonly string enSearch = "torrents.php?oldal={0}&tipus=kivalasztottak_kozott&kivalasztott_tipus=xvidser,dvdser,hdser&mire={1}&miben=name";
        private readonly string hunSearch = "torrents.php?oldal={0}&tipus=kivalasztottak_kozott&kivalasztott_tipus=xvidser_hun,dvdser_hun,hdser_hun,mire={1}&miben=name";
        private readonly string enHunSearch = "torrents.php?oldal={0}&tipus=kivalasztottak_kozott&kivalasztott_tipus=xvidser_hun,xvidser,dvdser_hun,dvdser,hdser_hun,hdser&mire={1}&miben=name";

        CookieContainer cookies;
        HttpClientHandler handler;
        HttpClient client;

        public nCore(IIndexerManagerService i, Logger l)
            : base(name: "nCore",
                description: "nCore qsdfqsdf",
                link: new Uri("http://ncore.cc/"),
                caps: TorznabCapsUtil.CreateDefaultTorznabTVCaps(),
                manager: i,
                logger: l)
        {
            SearchUrl = SiteLink + enHunSearch;

            if (ConfigData != null)
            {
                string hun, eng;
                Dictionary<string, string>[] configDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>[]>(this.ConfigData["config"].ToString());
                configDictionary[2].TryGetValue("value", out hun);
                configDictionary[3].TryGetValue("value", out eng);

                bool isHun = Boolean.Parse(hun);
                bool isEng = Boolean.Parse(eng);

                if (isHun && isEng)
                    SearchUrl = SiteLink + enHunSearch;
                else if (isHun && !isEng)
                    SearchUrl = SiteLink + hunSearch;
                else if (!isHun && isEng)
                    SearchUrl = SiteLink + enSearch;
            }

            cookies = new CookieContainer();
            handler = new HttpClientHandler
            {
                CookieContainer = cookies,
                AllowAutoRedirect = true,
                UseCookies = true,
            };
            client = new HttpClient(handler);
        }


        HttpRequestMessage CreateHttpRequest(string url)
        {
            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri(url);
            message.Headers.UserAgent.ParseAdd(BrowserUtil.ChromeUserAgent);
            return message;
        }

        public Task<ConfigurationData> GetConfigurationForSetup()
        {
            var config = this.ConfigData == null ? new ConfigurationDatanCore() : new ConfigurationDatanCore(this.ConfigData);
            return Task.FromResult<ConfigurationData>(config);
        }

        public async Task ApplyConfiguration(JToken configJson)
        {
            var config = new ConfigurationDatanCore();
            config.LoadValuesFromJson(configJson);

            if (config.Hungarian.Value == false && config.English.Value == false)
                throw new ExceptionWithConfigData("Please select atleast one language.", (ConfigurationData)config);

            var startMessage = CreateHttpRequest(LoginUrl);
            var results = await (await client.SendAsync(startMessage)).Content.ReadAsStringAsync();


            var pairs = new Dictionary<string, string> {
				{ "nev", config.Username.Value },
				{ "pass", config.Password.Value }
			};

            var content = new FormUrlEncodedContent(pairs);

            var loginRequest = CreateHttpRequest(LoginUrl);
            loginRequest.Method = HttpMethod.Post;
            loginRequest.Content = content;
            loginRequest.Headers.Referrer = new Uri(LoggedInUrl);

            var response = await client.SendAsync(loginRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!responseContent.Contains("Felhasználó"))
            {
                var errorMessage = "Couldn't login";
                throw new ExceptionWithConfigData(errorMessage, (ConfigurationData)config);
            }
            else
            {
                var configSaveData = new JObject();
                cookies.DumpToJson(SiteLink, configSaveData);
                cookies.DumpConfigToJson(config, configSaveData);
                SaveConfig(configSaveData);
                IsConfigured = true;
            }
        }

        async Task<ReleaseInfo[]> PerformQuery(TorznabQuery query, Uri baseUrl)
        {
            List<string> searchurls = new List<string>();
            var searchString = query.SanitizedSearchTerm + " " + query.GetEpisodeSearchString();
            searchurls.Add(String.Format(SearchUrl, 1, HttpUtility.UrlEncode(searchString)));
            searchurls = getSearchUrls(searchurls, searchString).Result;

            List<ReleaseInfo> releases = new List<ReleaseInfo>();
            foreach (string url in searchurls)
            {
                var results = await client.GetStringAsync(url);
                try
                {
                    CQ dom = results;

                    ReleaseInfo release;
                    var rows = dom[".box_torrent_all"].Find(".box_torrent");

                    foreach (var row in rows)
                    {
                        CQ qRow = row.Cq();

                        release = new ReleaseInfo();
                        var torrentTxt = qRow.Find(".torrent_txt").Find("a").Get(0);
                        if (torrentTxt == null) continue;
                        release.Title = torrentTxt.GetAttribute("title");
                        release.Description = release.Title;
                        release.MinimumRatio = 1;
                        release.MinimumSeedTime = 172800;

                        string downloadLink = SiteLink + torrentTxt.GetAttribute("href");
                        string downloadId = downloadLink.Substring(downloadLink.IndexOf("&id=") + 4);

                        release.Link = new Uri(SiteLink.ToString() + "torrents.php?action=download&id=" + downloadId);
                        release.Comments = new Uri(SiteLink.ToString() + "torrents.php?action=details&id=" + downloadId);
                        release.Guid = new Uri(release.Comments.ToString() + "#comments"); ;
                        release.Seeders = ParseUtil.CoerceInt(qRow.Find(".box_s2").Find("a").First().Text());
                        release.Peers = ParseUtil.CoerceInt(qRow.Find(".box_l2").Find("a").First().Text()) + release.Seeders;
                        release.PublishDate = DateTime.Parse(qRow.Find(".box_feltoltve2").Get(0).InnerHTML.Replace("<br />", " "), CultureInfo.InvariantCulture);
                        string[] sizeSplit = qRow.Find(".box_meret2").Get(0).InnerText.Split(' ');
                        release.Size = ReleaseInfo.GetBytes(sizeSplit[1].ToLower(), ParseUtil.CoerceFloat(sizeSplit[0]));

                        releases.Add(release);
                    }
                }
                catch (Exception ex)
                {
                    OnParseError(results, ex);
                }
            }

            return releases.ToArray();
        }

        private async Task<List<string>> getSearchUrls(List<string> searchurls, string searchString)
        {
            var results = await client.GetStringAsync(searchurls.First());
            CQ dom = results;
            var PagesCount = dom["#pager_top"].Find("a").Count();
            for (int i = 0; i < PagesCount; i++)
            {
                searchurls.Add(String.Format(SearchUrl, i + 2, HttpUtility.UrlEncode(searchString)));
                if (i == 2) break;
            }
            return searchurls;
        }

        public void LoadFromSavedConfiguration(JToken jsonConfig)
        {
            cookies.FillFromJson(SiteLink, jsonConfig, logger);
            IsConfigured = true;
        }

        public async Task<ReleaseInfo[]> PerformQuery(TorznabQuery query)
        {
            return await PerformQuery(query, SiteLink);
        }

        public Task<byte[]> Download(Uri link)
        {
            return client.GetByteArrayAsync(link);
        }
    }
}
