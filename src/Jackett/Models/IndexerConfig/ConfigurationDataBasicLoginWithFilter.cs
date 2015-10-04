﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jackett.Models.IndexerConfig
{
    public class ConfigurationDataBasicLoginWithFilter : ConfigurationData
    {
        public StringItem Username { get; private set; }
        public StringItem Password { get; private set; }
        public DisplayItem FilterExample { get; private set; }
        public StringItem FilterString { get; private set; }
        
        public ConfigurationDataBasicLoginWithFilter(string FilterInstructions)
        {
            Username = new StringItem { Name = "Username" };
            Password = new StringItem { Name = "Password" };
            FilterExample = new DisplayItem(FilterInstructions)
            {
                Name = ""
            };
            FilterString = new StringItem { Name = "Filters (optional)" };
        }


    }
}
