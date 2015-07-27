﻿using System;

namespace Jackett.Models
{
    public class ConfigurationDataUrl : ConfigurationData
    {
        public StringItem Url { get; private set; }

        public ConfigurationDataUrl(Uri defaultUrl) 
        {
            Url = new StringItem { Name = "Url", Value = defaultUrl.ToString() }; 
        }

        public ConfigurationDataUrl(string defaultUrl)
        {
            Url = new StringItem { Name = "Url", Value = defaultUrl };
        }

        public override Item[] GetItems()
        {
            return new Item[] { Url };
        }

        public string GetFormattedHostUrl()
        {
            var uri = new Uri(Url.Value);
            return string.Format("{0}://{1}", uri.Scheme, uri.Host);
        }
    }
}
