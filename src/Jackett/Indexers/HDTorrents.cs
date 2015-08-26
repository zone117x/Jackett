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
    public class HDTorrents : BaseIndexer, IIndexer
    {
        private string SearchUrl { get { return SiteLink + "torrents.php?"; } }
        private string LoginUrl { get { return SiteLink + "login.php"; } }
        private const int MAXPAGES = 3;

        new ConfigurationDataBasicLogin configData
        {
            get { return (ConfigurationDataBasicLogin)base.configData; }
            set { base.configData = value; }
        }

        public HDTorrents(IIndexerManagerService i, Logger l, IWebClient w, IProtectionService ps)
            : base(name: "HD-Torrents",
                description: "HD-Torrents is a private torrent website with HD torrents and strict rules on their content.",
                link: "http://hdts.ru/",// Of the accessible domains the .ru seems the most reliable.  https://hdts.ru | https://hd-torrents.org | https://hd-torrents.net | https://hd-torrents.me
                manager: i,
                client: w,
                logger: l,
                p: ps,
                configData: new ConfigurationDataBasicLogin())
        {
            TorznabCaps.Categories.Clear();

            AddCategoryMapping("1", TorznabCatType.MoviesHD);// Movie/Blu-Ray
            AddCategoryMapping("2", TorznabCatType.MoviesHD);// Movie/Remux
            AddCategoryMapping("5", TorznabCatType.MoviesHD);//Movie/1080p/i
            AddCategoryMapping("3", TorznabCatType.MoviesHD);//Movie/720p
            AddCategoryMapping("63", TorznabCatType.Audio);//Movie/Audio Track

            AddCategoryMapping("59", TorznabCatType.TVHD);//TV Show/Blu-ray
            AddCategoryMapping("60", TorznabCatType.TVHD);//TV Show/Remux
            AddCategoryMapping("30", TorznabCatType.TVHD);//TV Show/1080p/i
            AddCategoryMapping("38", TorznabCatType.TVHD);//TV Show/720p

            AddCategoryMapping("44", TorznabCatType.Audio);//Music/Album
            AddCategoryMapping("61", TorznabCatType.AudioVideo);//Music/Blu-Ray
            AddCategoryMapping("62", TorznabCatType.AudioVideo);//Music/Remux
            AddCategoryMapping("57", TorznabCatType.AudioVideo);//Music/1080p/i
            AddCategoryMapping("45", TorznabCatType.AudioVideo);//Music/720p

            AddCategoryMapping("58", TorznabCatType.XXX);//XXX/Blu-ray
            AddCategoryMapping("48", TorznabCatType.XXX);//XXX/1080p/i
            AddCategoryMapping("47", TorznabCatType.XXX);//XXX/720p
        }

        public async Task<IndexerConfigurationStatus> ApplyConfiguration(JToken configJson)
        {
            configData.LoadValuesFromJson(configJson);
            var loginPage = await RequestStringWithCookies(LoginUrl, string.Empty);

            var pairs = new Dictionary<string, string> {
                { "uid", configData.Username.Value },
                { "pwd", configData.Password.Value }
            };

            var result = await RequestLoginAndFollowRedirect(LoginUrl, pairs, loginPage.Cookies, true, null, LoginUrl);

            await ConfigureIfOK(result.Cookies, result.Content != null && result.Content.Contains("If your browser doesn't have javascript enabled"), () =>
            {
                var errorMessage = "Couldn't login";
                throw new ExceptionWithConfigData(errorMessage, configData);
            });
            return IndexerConfigurationStatus.RequiresTesting;
        }

        public async Task<IEnumerable<ReleaseInfo>> PerformQuery(TorznabQuery query)
        {
            var releases = new List<ReleaseInfo>();
            var searchurls = new List<string>();
            var searchUrl = SearchUrl;// string.Format(SearchUrl, HttpUtility.UrlEncode()));
            var queryCollection = new NameValueCollection();
            var searchString = query.GetQueryString();


            foreach (var cat in MapTorznabCapsToTrackers(query))
            {
                searchUrl += "category%5B%5D=" + cat + "&";
            }


            if (!string.IsNullOrWhiteSpace(searchString))
            {
                queryCollection.Add("search", searchString);
            }



            queryCollection.Add("active", "1");
            queryCollection.Add("options", "0");

            searchUrl += queryCollection.GetQueryString();


            var results = await RequestStringWithCookiesAndRetry(searchUrl);
            try
            {
                CQ dom = results.Content;
                ReleaseInfo release;

                int rowCount = 0;
                var rows = dom[".mainblockcontenttt > tbody > tr"];
                foreach (var row in rows)
                {
                    CQ qRow = row.Cq();
                    if (rowCount < 2 || qRow.Children().Count() != 12) //skip 2 rows because there's an empty row & a title/sort row
                    {
                        rowCount++;
                        continue;
                    }

                    release = new ReleaseInfo();

                    release.Title = qRow.Find("td.mainblockcontent b a").Text();
                    release.Description = release.Title;

                    if (0 != qRow.Find("td.mainblockcontent u").Length)
                    {
                        var imdbStr = qRow.Find("td.mainblockcontent u").Parent().First().Attr("href").Replace("http://www.imdb.com/title/tt", "").Replace("/", "");
                        long imdb;
                        if (ParseUtil.TryCoerceLong(imdbStr, out imdb))
                        {
                            release.Imdb = imdb;
                        }
                    }

                    release.MinimumRatio = 1;
                    release.MinimumSeedTime = 172800;



                    int seeders, peers;
                    if (ParseUtil.TryCoerceInt(qRow.Find("td").Get(9).FirstChild.FirstChild.InnerText, out seeders))
                    {
                        release.Seeders = seeders;
                        if (ParseUtil.TryCoerceInt(qRow.Find("td").Get(10).FirstChild.FirstChild.InnerText, out peers))
                        {
                            release.Peers = peers + release.Seeders;
                        }
                    }

                    string fullSize = qRow.Find("td.mainblockcontent").Get(6).InnerText;
                    release.Size = ReleaseInfo.GetBytes(fullSize);

                    release.Guid = new Uri(SiteLink + qRow.Find("td.mainblockcontent b a").Attr("href"));
                    release.Link = new Uri(SiteLink + qRow.Find("td.mainblockcontent").Get(3).FirstChild.GetAttribute("href"));
                    release.Comments = new Uri(SiteLink + qRow.Find("td.mainblockcontent b a").Attr("href") + "#comments");

                    string[] dateSplit = qRow.Find("td.mainblockcontent").Get(5).InnerHTML.Split(',');
                    string dateString = dateSplit[1].Substring(0, dateSplit[1].IndexOf('>'));
                    release.PublishDate = DateTime.Parse(dateString, CultureInfo.InvariantCulture);

                    string category = qRow.Find("td:eq(0) a").Attr("href").Replace("torrents.php?category=", "");
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
