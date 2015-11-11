﻿using Autofac;
using AutoMapper;
using Jackett.Indexers;
using Jackett.Models;
using Jackett.Services;
using Jackett.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using System.Web.Security;
using System.Windows.Forms;

namespace Jackett.Controllers
{
    [RoutePrefix("admin")]
    [JackettAuthorized]
    [JackettAPINoCache]
    public class AdminController : ApiController
    {
        private IConfigurationService config;
        private IIndexerManagerService indexerService;
        private IServerService serverService;
        private ISecuityService securityService;
        private IProcessService processService;
        private ICacheService cacheService;
        private Logger logger;
        private ILogCacheService logCache;

        public AdminController(IConfigurationService config, IIndexerManagerService i, IServerService ss, ISecuityService s, IProcessService p, ICacheService c, Logger l, ILogCacheService lc)
        {
            this.config = config;
            indexerService = i;
            serverService = ss;
            securityService = s;
            processService = p;
            cacheService = c;
            logger = l;
            logCache = lc;
        }

        private async Task<JToken> ReadPostDataJson()
        {
            var content = await Request.Content.ReadAsStringAsync();
            return JObject.Parse(content);
        }


        private HttpResponseMessage GetFile(string path)
        {
            var result = new HttpResponseMessage(HttpStatusCode.OK);
            var mappedPath = Path.Combine(config.GetContentFolder(), path);
            var stream = new FileStream(mappedPath, FileMode.Open);
            result.Content = new StreamContent(stream);
            result.Content.Headers.ContentType =
                new MediaTypeHeaderValue(MimeMapping.GetMimeMapping(mappedPath));

            return result;
        }

        [HttpGet]
        [AllowAnonymous]
        public RedirectResult Logout()
        {
            var ctx = Request.GetOwinContext();
            var authManager = ctx.Authentication;
            authManager.SignOut("ApplicationCookie");
            return Redirect("/Admin/Dashboard");
        }

        [HttpGet]
        [HttpPost]
        [AllowAnonymous]
        public async Task<HttpResponseMessage> Dashboard()
        {
            if (Request.RequestUri.Query != null && Request.RequestUri.Query.Contains("logout"))
            {
                var file = GetFile("login.html");
                securityService.Logout(file);
                return file;
            }


            if (securityService.CheckAuthorised(Request))
            {
                return GetFile("index.html");

            }
            else
            {
                var formData = await Request.Content.ReadAsFormDataAsync();

                if (formData != null && securityService.HashPassword(formData["password"]) == serverService.Config.AdminPassword)
                {
                    var file = GetFile("index.html");
                    securityService.Login(file);
                    return file;
                }
                else
                {
                    return GetFile("login.html");
                }
            }
        }

        [Route("set_admin_password")]
        [HttpPost]
        public async Task<IHttpActionResult> SetAdminPassword()
        {
            var jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson();
                var password = (string)postData["password"];
                if (string.IsNullOrEmpty(password))
                {
                    serverService.Config.AdminPassword = string.Empty;
                }
                else
                {
                    serverService.Config.AdminPassword = securityService.HashPassword(password);
                }

                serverService.SaveConfig();
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception in SetAdminPassword");
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;
            }
            return Json(jsonReply);
        }

        [Route("get_config_form")]
        [HttpPost]
        public async Task<IHttpActionResult> GetConfigForm()
        {
            var jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson();
                var indexer = indexerService.GetIndexer((string)postData["indexer"]);
                var config = await indexer.GetConfigurationForSetup();
                jsonReply["config"] = config.ToJson(null);
                jsonReply["caps"] = indexer.TorznabCaps.CapsToJson();
                jsonReply["name"] = indexer.DisplayName;
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception in GetConfigForm");
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;
            }
            return Json(jsonReply);
        }

        [Route("configure_indexer")]
        [HttpPost]
        public async Task<IHttpActionResult> Configure()
        {
            var jsonReply = new JObject();
            IIndexer indexer = null;
            try
            {
                var postData = await ReadPostDataJson();
                string indexerString = (string)postData["indexer"];
                indexer = indexerService.GetIndexer((string)postData["indexer"]);
                jsonReply["name"] = indexer.DisplayName;
                var configurationResult = await indexer.ApplyConfiguration(postData["config"]);
                if (configurationResult == IndexerConfigurationStatus.RequiresTesting)
                {
                    await indexerService.TestIndexer((string)postData["indexer"]);
                }
                else if (configurationResult == IndexerConfigurationStatus.Failed)
                {
                    throw new Exception("Configuration Failed");
                }
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;
                var baseIndexer = indexer as BaseIndexer;
                if (null != baseIndexer)
                    baseIndexer.ResetBaseConfig();
                if (ex is ExceptionWithConfigData)
                {
                    jsonReply["config"] = ((ExceptionWithConfigData)ex).ConfigData.ToJson(null,false);
                }
                else
                {
                    logger.Error(ex, "Exception in Configure");
                }
            }
            return Json(jsonReply);
        }

        [Route("get_indexers")]
        [HttpGet]
        public IHttpActionResult Indexers()
        {
            var jsonReply = new JObject();
            try
            {
                jsonReply["result"] = "success";
                JArray items = new JArray();

                foreach (var indexer in indexerService.GetAllIndexers())
                {
                    var item = new JObject();
                    item["id"] = indexer.ID;
                    item["name"] = indexer.DisplayName;
                    item["description"] = indexer.DisplayDescription;
                    item["configured"] = indexer.IsConfigured;
                    item["site_link"] = indexer.SiteLink;
                    item["potatoenabled"] = indexer.TorznabCaps.Categories.Select(c => c.ID).Any(i => PotatoController.MOVIE_CATS.Contains(i));

                    var caps = new JObject();
                    foreach (var cap in indexer.TorznabCaps.Categories)
                        caps[cap.ID.ToString()] = cap.Name;
                    item["caps"] = caps;
                    items.Add(item);
                }
                jsonReply["items"] = items;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception in get_indexers");
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;
            }
            return Json(jsonReply);
        }

        [Route("test_indexer")]
        [HttpPost]
        public async Task<IHttpActionResult> Test()
        {
            JToken jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson();
                string indexerString = (string)postData["indexer"];
                await indexerService.TestIndexer(indexerString);
                jsonReply["name"] = indexerService.GetIndexer(indexerString).DisplayName;
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception in test_indexer");
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;
            }
            return Json(jsonReply);
        }

        [Route("delete_indexer")]
        [HttpPost]
        public async Task<IHttpActionResult> Delete()
        {
            var jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson();
                string indexerString = (string)postData["indexer"];
                indexerService.DeleteIndexer(indexerString);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception in delete_indexer");
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;
            }
            return Json(jsonReply);
        }

        [Route("get_jackett_config")]
        [HttpGet]
        public IHttpActionResult GetConfig()
        {
            var jsonReply = new JObject();
            try
            {
                var cfg = new JObject();
                cfg["port"] = serverService.Config.Port;
                cfg["external"] = serverService.Config.AllowExternal;
                cfg["api_key"] = serverService.Config.APIKey;
                cfg["blackholedir"] = serverService.Config.BlackholeDir;
                cfg["password"] = string.IsNullOrEmpty(serverService.Config.AdminPassword) ? string.Empty : serverService.Config.AdminPassword.Substring(0, 10);

                jsonReply["config"] = cfg;
                jsonReply["app_version"] = config.GetVersion();
                jsonReply["result"] = "success";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception in get_jackett_config");
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;
            }
            return Json(jsonReply);
        }

        [Route("set_config")]
        [HttpPost]
        public async Task<IHttpActionResult> SetConfig()
        {
            var originalPort = Engine.Server.Config.Port;
            var originalAllowExternal = Engine.Server.Config.AllowExternal;
            var jsonReply = new JObject();
            try
            {
                var postData = await ReadPostDataJson();
                int port = (int)postData["port"];
                bool external = (bool)postData["external"];
                string saveDir = (string)postData["blackholedir"];

                if (port != Engine.Server.Config.Port || external != Engine.Server.Config.AllowExternal)
                {
                    if (ServerUtil.RestrictedPorts.Contains(port))
                    {
                        jsonReply["result"] = "error";
                        jsonReply["error"] = "The port you have selected is restricted, try a different one.";
                        return Json(jsonReply);
                    }

                    // Save port to the config so it can be picked up by the if needed when running as admin below.
                    Engine.Server.Config.AllowExternal = external;
                    Engine.Server.Config.Port = port;
                    Engine.Server.SaveConfig();

                    // On Windows change the url reservations
                    if (System.Environment.OSVersion.Platform != PlatformID.Unix)
                    {
                        if (!ServerUtil.IsUserAdministrator())
                        {
                            try
                            {
                                processService.StartProcessAndLog(Application.ExecutablePath, "--ReserveUrls", true);
                            }
                            catch
                            {
                                Engine.Server.Config.Port = originalPort;
                                Engine.Server.Config.AllowExternal = originalAllowExternal;
                                Engine.Server.SaveConfig();
                                jsonReply["result"] = "error";
                                jsonReply["error"] = "Failed to acquire admin permissions to reserve the new port.";
                                return Json(jsonReply);
                            }
                        }
                        else
                        {
                            serverService.ReserveUrls(true);
                        }
                    }

                    (new Thread(() =>
                    {
                        Thread.Sleep(500);
                        serverService.Stop();
                        Engine.BuildContainer();
                        Engine.Server.Initalize();
                        Engine.Server.Start();
                    })).Start();
                }


                if (saveDir != Engine.Server.Config.BlackholeDir)
                {
                    if (!string.IsNullOrEmpty(saveDir))
                    {
                        if (!Directory.Exists(saveDir))
                        {
                            throw new Exception("Blackhole directory does not exist");
                        }
                    }

                    Engine.Server.Config.BlackholeDir = saveDir;
                    Engine.Server.SaveConfig();
                }

                jsonReply["result"] = "success";
                jsonReply["port"] = port;
                jsonReply["external"] = external;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception in set_port");
                jsonReply["result"] = "error";
                jsonReply["error"] = ex.Message;
            }
            return Json(jsonReply);
        }

        [Route("GetCache")]
        [HttpGet]
        public List<TrackerCacheResult> GetCache()
        {
            var results = cacheService.GetCachedResults();
            ConfigureCacheResults(results);
            return results;
        }


        private void ConfigureCacheResults(List<TrackerCacheResult> results)
        {
            var serverUrl = string.Format("{0}://{1}:{2}/", Request.RequestUri.Scheme, Request.RequestUri.Host, Request.RequestUri.Port);
            foreach (var result in results)
            {
                var link = result.Link;
                result.Link = serverService.ConvertToProxyLink(link, serverUrl, result.TrackerId, "dl", result.Title + ".torrent");
                if (result.Link != null && result.Link.Scheme != "magnet" && !string.IsNullOrWhiteSpace(Engine.Server.Config.BlackholeDir))
                    result.BlackholeLink = serverService.ConvertToProxyLink(link, serverUrl, result.TrackerId, "bh", string.Empty);

            }
        }

        [Route("GetLogs")]
        [HttpGet]
        public List<CachedLog> GetLogs()
        {
            return logCache.Logs;
        }

        [Route("Search")]
        [HttpPost]
        public ManualSearchResult Search([FromBody]AdminSearch value)
        {
            var results = new List<TrackerCacheResult>();
            var query = new TorznabQuery()
            {
                SearchTerm = value.Query,
                Categories = value.Category == 0 ? new int[0] : new int[1] { value.Category }
            };

            query.ExpandCatsToSubCats();

            var trackers = indexerService.GetAllIndexers().Where(t => t.IsConfigured).ToList();
            if (!string.IsNullOrWhiteSpace(value.Tracker))
            {
                trackers = trackers.Where(t => t.ID == value.Tracker).ToList();
            }

            if (value.Category != 0)
            {
                trackers = trackers.Where(t => t.TorznabCaps.Categories.Select(c => c.ID).Contains(value.Category)).ToList();
            }

            Parallel.ForEach(trackers.ToList(), indexer =>
            {
                try
                {
                    var searchResults = indexer.PerformQuery(query).Result;
                    searchResults = indexer.CleanLinks(searchResults);
                    cacheService.CacheRssResults(indexer, searchResults);
                    searchResults = indexer.FilterResults(query, searchResults);

                    lock (results)
                    {
                        foreach (var result in searchResults)
                        {
                            var item = Mapper.Map<TrackerCacheResult>(result);
                            item.Tracker = indexer.DisplayName;
                            item.TrackerId = indexer.ID;
                            item.Peers = item.Peers - item.Seeders; // Use peers as leechers
                            results.Add(item);
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error occured during manual search on " + indexer.DisplayName + ":  " + e.Message);
                }
            });

            ConfigureCacheResults(results);

            if (trackers.Count > 1)
            {
                results = results.OrderByDescending(d => d.PublishDate).ToList();
            }

            var manualResult = new ManualSearchResult()
            {
                Results = results,
                Indexers = trackers.Select(t => t.DisplayName).ToList()
            };


            if (manualResult.Indexers.Count == 0)
                manualResult.Indexers = new List<string>() { "None" };

            logger.Info(string.Format("Manual search for \"{0}\" on {1} with {2} results.", query.GetQueryString(), string.Join(", ", manualResult.Indexers), manualResult.Results.Count));
            return manualResult;
        }
    }
}

