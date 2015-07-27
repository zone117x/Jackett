﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Jackett.Models
{
    public class TorznabCapabilities
    {

        public bool SearchAvailable { get; set; }

        public bool TVSearchAvailable { get; set; }

        public bool SupportsTVRageSearch { get; set; }

        public List<TorznabCategory> Categories { get; private set; }

        public TorznabCapabilities()
        {
            Categories = new List<TorznabCategory>();
        }

        string SupportedTVSearchParams
        {
            get
            {
                var parameters = new List<string>() { "q", "season", "ep" };
                if (SupportsTVRageSearch)
                    parameters.Add("rid");
                return string.Join(",", parameters);
            }
        }

        public string ToXml()
        {
            var xdoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("caps",
                    new XElement("searching",
                        new XElement("search",
                            new XAttribute("available", SearchAvailable ? "yes" : "no"),
                            new XAttribute("supportedParams", "q")
                        ),
                        new XElement("tv-search",
                            new XAttribute("available", TVSearchAvailable ? "yes" : "no"),
                            new XAttribute("supportedParams", SupportedTVSearchParams)
                        )
                    ),
                    new XElement("categories",
                        from c in Categories
                        select new XElement("category",
                            new XAttribute("id", c.ID),
                            new XAttribute("name", c.Name),
                            from sc in c.SubCategories
                            select new XElement("subcat",
                                new XAttribute("id", sc.ID),
                                new XAttribute("name", sc.Name)
                            )
                        )
                    )
                )
            );

            return xdoc.Declaration.ToString() + Environment.NewLine + xdoc.ToString();
        }
    }
}
