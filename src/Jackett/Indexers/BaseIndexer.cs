﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jackett.Models;
using Newtonsoft.Json.Linq;
using NLog;
using Jackett.Services;

namespace Jackett.Indexers
{
    public abstract class BaseIndexer
    {
        public string DisplayDescription { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsConfigured { get; protected set; }
        public Uri SiteLink { get; private set; }
        public bool RequiresRageIDLookupDisabled { get; private set; }

        protected Logger logger;
        protected IIndexerManagerService indexerService;

        protected static List<CachedResult> cache = new List<CachedResult>();
        protected static readonly TimeSpan cacheTime = new TimeSpan(0, 9, 0);


        public BaseIndexer(string name, string description, bool rageid, Uri link, IIndexerManagerService manager, Logger logger)
        {
            DisplayName = name;
            DisplayDescription = description;
            SiteLink = link;
            this.logger = logger;
            indexerService = manager;
            RequiresRageIDLookupDisabled = rageid;
        }

        protected void SaveConfig(JToken config)
        {
            indexerService.SaveConfig(this as IIndexer, config);
        }

        protected void OnParseError(string results, Exception ex)
        {
            var fileName = string.Format("Error on {0} for {1}.txt", DateTime.Now.ToString("yyyyMMddHHmmss"), DisplayName);
            var spacing = string.Join("", Enumerable.Repeat(Environment.NewLine, 5));
            var fileContents = string.Format("{0}{1}{2}", ex, spacing, results);
            logger.Error(fileName + fileContents);
            throw ex;
        }

        protected void CleanCache()
        {
            foreach (var expired in cache.Where(i => i.Created - DateTime.Now > cacheTime).ToList())
            {
                cache.Remove(expired);
            }
        }
    }
}