using CsQuery;
using Jackett.Indexers;
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
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;
using CsQuery.ExtensionMethods;
using Jackett.Models.IndexerConfig;

namespace Jackett.Indexers
{
    public class DanishBits : BaseIndexer, IIndexer
    {
        private string LoginUrl { get { return SiteLink + "login.php"; } }
        private string SearchUrl { get { return SiteLink + "torrents.php"; } }

        new NxtGnConfigurationData configData
        {
            get { return (NxtGnConfigurationData)base.configData; }
            set { base.configData = value; }
        }

        public DanishBits(IIndexerManagerService i, Logger l, IWebClient c, IProtectionService ps)
            : base(name: "DanishBits",
                description: "A danish closed torrent tracker",
                link: "https://danishbits.org/",
                caps: TorznabUtil.CreateDefaultTorznabTVCaps(),
                manager: i,
                client: c,
                logger: l,
                p: ps,
                configData: new NxtGnConfigurationData())
        {
            // Movies Mapping
            // DanishBits HD
            AddCategoryMapping(2, TorznabCatType.MoviesHD);
            AddCategoryMapping(2, TorznabCatType.MoviesWEBDL);

            // Danske film
            AddCategoryMapping(3, TorznabCatType.MoviesHD);
            AddCategoryMapping(3, TorznabCatType.MoviesWEBDL);
            AddCategoryMapping(3, TorznabCatType.MoviesDVD);
            AddCategoryMapping(3, TorznabCatType.MoviesForeign);
            AddCategoryMapping(3, TorznabCatType.MoviesSD);

            // DVDR Nordic
            AddCategoryMapping(10, TorznabCatType.MoviesDVD);
            AddCategoryMapping(10, TorznabCatType.MoviesForeign);

            // Custom
            AddCategoryMapping(28, TorznabCatType.MoviesHD);
            AddCategoryMapping(28, TorznabCatType.MoviesDVD);

            // Custom HD
            AddCategoryMapping(29, TorznabCatType.MoviesHD);
            AddCategoryMapping(29, TorznabCatType.MoviesWEBDL);

            // Custom Tablet
            AddCategoryMapping(31, TorznabCatType.MoviesSD);

            if (!configData.OnlyDanishCategories.Value)
            {
                // Bluray
                AddCategoryMapping(8, TorznabCatType.MoviesBluRay);

                // Boxset
                AddCategoryMapping(9, TorznabCatType.MoviesHD);
                AddCategoryMapping(9, TorznabCatType.MoviesForeign);
                AddCategoryMapping(9, TorznabCatType.MoviesDVD);

                // DVDR
                AddCategoryMapping(11, TorznabCatType.MoviesDVD);

                // HDx264
                AddCategoryMapping(22, TorznabCatType.MoviesHD);

                // XviD/MP4/SDx264
                AddCategoryMapping(24, TorznabCatType.MoviesSD);
            }

            // TV Mapping
            // DanishBits TV
            AddCategoryMapping(1, TorznabCatType.TVHD);
            AddCategoryMapping(1, TorznabCatType.TVWEBDL);

            // Dansk TV
            AddCategoryMapping(4, TorznabCatType.TVHD);
            AddCategoryMapping(4, TorznabCatType.TVWEBDL);
            AddCategoryMapping(4, TorznabCatType.TVFOREIGN);
            AddCategoryMapping(4, TorznabCatType.TVSD);

            // Custom TV
            AddCategoryMapping(30, TorznabCatType.TVHD);
            AddCategoryMapping(30, TorznabCatType.TVWEBDL);

            if (!configData.OnlyDanishCategories.Value)
            {
                // TV
                AddCategoryMapping(20, TorznabCatType.TVHD);
                AddCategoryMapping(20, TorznabCatType.TVSD);
                AddCategoryMapping(20, TorznabCatType.TVWEBDL);

                // TV Boxset
                AddCategoryMapping(21, TorznabCatType.TVHD);
                AddCategoryMapping(21, TorznabCatType.TVSD);
                AddCategoryMapping(21, TorznabCatType.TVWEBDL);
            }

            // E-book
            AddCategoryMapping(12, TorznabCatType.BooksEbook);
            // Audiobooks
            AddCategoryMapping(6, TorznabCatType.AudioAudiobook);
        }

        public async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            configData.LoadValuesFromJson(configJson);
            var pairs = new Dictionary<string, string> {
                { "username", configData.Username.Value },
                { "password", configData.Password.Value },
                { "langlang", null },
                { "login", "login" }
            };
            // Get inital cookies
            CookieHeader = string.Empty;
            var response = await RequestLoginAndFollowRedirect(LoginUrl, pairs, CookieHeader, true, null, LoginUrl);

            await ConfigureIfOK(response.Cookies, response.Content != null && response.Content.Contains("logout.php"), () =>
            {
                CQ dom = response.Content;
                var messageEl = dom["#loginform .warning"];
                var errorMessage = messageEl.Text().Trim();
                throw new ExceptionWithConfigData(errorMessage, configData);
            });
            return IndexerConfigurationStatus.RequiresTesting;
        }

        public async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            var releasesPerPage = 100;
            var releases = new List<ReleaseInfo>();

            var page = (query.Offset/releasesPerPage) + 1;

            string episodeSearchUrl;
            if (string.IsNullOrEmpty(query.GetQueryString()))
            {
                episodeSearchUrl = SearchUrl + "?page=" + page;
            }
            else
            {
                var cats = MapTorznabCapsToTrackers(query);
                var catsUrlPart = string.Join("&", cats.Select(c => $"filter_{c}=on"));
                episodeSearchUrl = $"{SearchUrl}?page={page}&group=0&{catsUrlPart}&search={HttpUtility.UrlEncode(query.GetQueryString())}&pre_type=torrents&type=";
            }
            var results = await RequestStringWithCookiesAndRetry(episodeSearchUrl);
            if (string.IsNullOrEmpty(results.Content))
            {
                CookieHeader = string.Empty;
                var pairs = new Dictionary<string, string>
                {
                    {"username", configData.Username.Value},
                    {"password", configData.Password.Value},
                    {"langlang", null},
                    {"login", "login"}
                };
                var response = await RequestLoginAndFollowRedirect(LoginUrl, pairs, CookieHeader, true, null, LoginUrl);

                await ConfigureIfOK(response.Cookies, response.Content != null && response.Content.Contains("logout.php"), () =>
                {
                    CQ dom = response.Content;
                    var messageEl = dom["#loginform .warning"];
                    var errorMessage = messageEl.Text().Trim();
                    throw new ExceptionWithConfigData(errorMessage, configData);
                });
                results = await RequestStringWithCookiesAndRetry(episodeSearchUrl);
            }
            try
            {
                CQ dom = results.Content;
                var rows = dom["#torrent_table tr.torrent"];
                foreach (var row in rows)
                {
                    var qRow = row.Cq();
                    var release = new ReleaseInfo
                    {
                        MinimumRatio = 1,
                        MinimumSeedTime = 172800
                    };

                    var catAnchor = row.FirstChild.FirstChild;
                    var catUrl = catAnchor.GetAttribute("href");
                    var catStr = Regex.Match(catUrl, "filter_(?<catNo>[0-9]+)=on").Groups["catNo"].Value;
                    var catNo = int.Parse(catStr);
                    var moviesCatsDanish = new[] { 2,3,10,28,29,31 };
                    var moviesCatsIntl = new[] { 8,9,11,22,24 };
                    var moviesCats = configData.OnlyDanishCategories.Value
                        ? moviesCatsDanish
                        : moviesCatsDanish.Concat(moviesCatsIntl);
                    var seriesCatsDanish = new[] { 1,4,30 };
                    var seriesCatsIntl = new[] { 20,21 };
                    var seriesCats = configData.OnlyDanishCategories.Value
                        ? seriesCatsDanish
                        : seriesCatsDanish.Concat(seriesCatsIntl);
                    if (moviesCats.Contains(catNo))
                        release.Category = TorznabCatType.Movies.ID;
                    else if (seriesCats.Contains(catNo))
                        release.Category = TorznabCatType.TV.ID;
                    else if (catNo == 12)
                        release.Category = TorznabCatType.BooksEbook.ID;
                    else if (catNo == 6)
                        release.Category = TorznabCatType.AudioAudiobook.ID;
                    else
                        continue;

                    var titleAnchor = qRow.Find("div.croptorrenttext a").FirstElement();
                    var title = titleAnchor.GetAttribute("title");
                    release.Title = title;

                    var dlUrlAnchor = qRow.Find("span.right a").FirstElement();
                    var dlUrl = dlUrlAnchor.GetAttribute("href");
                    var authkey = Regex.Match(dlUrl, "authkey=(?<authkey>[0-9a-zA-Z]+)").Groups["authkey"].Value;
                    var torrentPass = Regex.Match(dlUrl, "torrent_pass=(?<torrent_pass>[0-9a-zA-Z]+)").Groups["torrent_pass"].Value;
                    var torrentId = Regex.Match(dlUrl, "id=(?<id>[0-9]+)").Groups["id"].Value;
                    release.Link = new Uri($"{SearchUrl}/{title}.torrent/?action=download&authkey={authkey}&torrent_pass={torrentPass}&id={torrentId}");

                    var torrentLink = titleAnchor.GetAttribute("href");
                    release.Guid = new Uri(SiteLink + torrentLink);
                    release.Comments = new Uri(SearchUrl + torrentLink);

                    var addedElement = qRow.Find("span.time").FirstElement();
                    var addedStr = addedElement.GetAttribute("title");
                    release.PublishDate = DateTime.ParseExact(addedStr, "MMM dd yyyy, HH:mm",
                        CultureInfo.InvariantCulture);

                    var columns = qRow.Children();
                    var seedersElement = columns.Reverse().Skip(1).First();
                    release.Seeders = int.Parse(seedersElement.InnerText);

                    var leechersElement = columns.Last().FirstElement();
                    release.Peers = release.Seeders + int.Parse(leechersElement.InnerText);

                    var sizeElement = columns.Skip(2).First();
                    var sizeStr = sizeElement.InnerText;
                    release.Size = ReleaseInfo.GetBytes(sizeStr);

                    var imdbAnchor = qRow.Find(".torrentnotes a")
                        .FirstOrDefault(a => a.GetAttribute("href").Contains("imdb.com"));
                    if (imdbAnchor != null)
                    {
                        var referrerUrl = imdbAnchor.GetAttribute("href");
                        release.Imdb = long.Parse(Regex.Match(referrerUrl, "tt(?<imdbId>[0-9]+)").Groups["imdbId"].Value);
                    }
                    releases.Add(release);
                }
            }
            catch (Exception ex)
            {
                OnParseError(results.Content, ex);
            }
            return releases;
        }

        public class NxtGnConfigurationData : ConfigurationData
        {
            public NxtGnConfigurationData()
            {
                Username = new StringItem { Name = "Username" };
                Password = new StringItem { Name = "Password" };
                OnlyDanishCategories = new BoolItem { Name = "Only Danish Categories" };
            }
            public StringItem Username { get; private set; }
            public StringItem Password { get; private set; }
            public BoolItem OnlyDanishCategories { get; private set; }
        }
    }
}
