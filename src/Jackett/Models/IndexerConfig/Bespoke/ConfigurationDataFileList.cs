﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jackett.Models.IndexerConfig.Bespoke
{
    class ConfigurationDataFileList : ConfigurationDataBasicLogin
    {
        public BoolItem IncludeRomanianReleases { get; private set; }
        public DisplayItem CatWarning { get; private set; }

        public ConfigurationDataFileList()
            : base()
        {
            IncludeRomanianReleases = new BoolItem() { Name = "IncludeRomanianReleases", Value = false };
            CatWarning = new DisplayItem("When mapping TV ensure you add category 5000 in addition to 5030,5040.") { Name = "CatWarning" };
        }
    }
}
