﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace Jackett.Models
{
    public class ResultPage
    {
        static XNamespace atomNs = "http://www.w3.org/2005/Atom";
        static XNamespace torznabNs = "http://torznab.com/schemas/2015/feed";

        public ChannelInfo ChannelInfo { get; private set; }
        public List<ReleaseInfo> Releases { get; private set; }

        public ResultPage(ChannelInfo channelInfo)
        {
            ChannelInfo = channelInfo;
            Releases = new List<ReleaseInfo>();
        }

        string xmlDateFormat(DateTime dt)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            //Sat, 14 Mar 2015 17:10:42 -0400
            var f = string.Format(@"{0:ddd, dd MMM yyyy HH:mm:ss }{1}", dt, string.Format("{0:zzz}", dt).Replace(":", ""));
            return f;
        }

        XElement getTorznabElement(string name, object value)
        {
            return value == null ? null : new XElement(torznabNs + "attr", new XAttribute("name", name), new XAttribute("value", value));
        }

        public string ToXml(Uri selfAtom)
        {
            var xdoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("rss",
                    new XAttribute("version", "1.0"),
                    new XAttribute(XNamespace.Xmlns + "atom", atomNs.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "torznab", torznabNs.NamespaceName),
                    new XElement("channel",
                        new XElement(atomNs + "link",
                            new XAttribute("href", selfAtom.ToString()),
                            new XAttribute("rel", "self"),
                            new XAttribute("type", "application/rss+xml")
                        ),
                        new XElement("title", ChannelInfo.Title),
                        new XElement("description", ChannelInfo.Description),
                        new XElement("link", ChannelInfo.Link),
                        new XElement("lanuage", ChannelInfo.Language),
                        new XElement("category", ChannelInfo.Category),
                        new XElement("image",
                            new XElement("url", ChannelInfo.ImageUrl.ToString()),
                            new XElement("title", ChannelInfo.ImageTitle),
                            new XElement("link", ChannelInfo.ImageLink.ToString()),
                            new XElement("description", ChannelInfo.ImageDescription)
                        ),


                        from r in Releases
                        select new XElement("item",
                            new XElement("title", r.Title),
                            new XElement("guid", r.Guid),
                            r.Comments == null ? null : new XElement("comments", r.Comments.ToString()),
                            r.PublishDate == DateTime.MinValue ? null : new XElement("pubDate", xmlDateFormat(r.PublishDate)),
                            r.Size == null ? null : new XElement("size", r.Size),
                            new XElement("description", r.Description),
                            new XElement("link", r.Link ?? r.MagnetUri),
                            r.Category == null ? null : new XElement("category", r.Category),
                            new XElement(
                                "enclosure",
                                new XAttribute("url", r.Link ?? r.MagnetUri),
                                r.Size == null ? null : new XAttribute("length", r.Size),
                                new XAttribute("type", "application/x-bittorrent")
                            ),
                            getTorznabElement("magneturl", r.MagnetUri),
                            getTorznabElement("rageid", r.RageID),
                            getTorznabElement("seeders", r.Seeders),
                            getTorznabElement("peers", r.Peers),
                            getTorznabElement("infohash", r.InfoHash),
                            getTorznabElement("minimumratio", r.MinimumRatio),
                            getTorznabElement("minimumseedtime", r.MinimumSeedTime)
                        )
                    )
                )
            );

            return xdoc.Declaration.ToString() + Environment.NewLine + xdoc.ToString();
        }
    }
}
