﻿using AutoMapper;
using Jackett.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jackett.Services
{
    public interface ICacheService
    {
        void CacheRssResults(string trackerId, ReleaseInfo[] releases);
        List<TrackerCacheResult> GetCachedResults();
    }

    public class CacheService : ICacheService
    {
        private readonly List<TrackerCache> cache = new List<TrackerCache>();
        private readonly int MAX_RESULTS_PER_TRACKER = 100;
        private readonly TimeSpan AGE_LIMIT = new TimeSpan(2, 0, 0, 0);

        static CacheService()
        {
            Mapper.CreateMap<ReleaseInfo,TrackerCacheResult>();
        }

        public void CacheRssResults(string trackerId, ReleaseInfo[] releases)
        {
            lock (cache)
            {
                var trackerCache = cache.Where(c => c.TrackerId == trackerId).FirstOrDefault();
                if (trackerCache == null)
                {
                    trackerCache = new TrackerCache();
                    trackerCache.TrackerId = trackerId;
                    cache.Add(trackerCache);
                }

                foreach(var release in releases.OrderByDescending(i=>i.PublishDate))
                {
                    // Skip old releases
                    if(release.PublishDate-DateTime.Now> AGE_LIMIT)
                    {
                        continue;
                    }

                    var existingItem = trackerCache.Results.Where(i => i.Result.Guid == release.Guid).FirstOrDefault();
                    if (existingItem == null)
                    {
                        existingItem = new CachedResult();
                        existingItem.Created = DateTime.Now;
                        trackerCache.Results.Add(existingItem);
                    }

                    existingItem.Result = release;
                }

                // Prune cache
                foreach(var tracker in cache)
                {
                    tracker.Results = tracker.Results.OrderByDescending(i => i.Created).Take(MAX_RESULTS_PER_TRACKER).ToList();
                }
            }
        }

        public List<TrackerCacheResult> GetCachedResults()
        {
            lock (cache)
            {
                var results = new List<TrackerCacheResult>();

                foreach(var tracker in cache)
                {
                    foreach(var release in tracker.Results)
                    {
                        var item = Mapper.Map<TrackerCacheResult>(release.Result);
                        item.FirstSeen = release.Created;
                        item.Tracker = tracker.TrackerId;
                        item.Peers = item.Peers - item.Seeders; // Use peers as leechers
                        results.Add(item);
                    }
                }

                return results.OrderByDescending(i=>i.PublishDate).ToList();
            }
        }
    }
}
