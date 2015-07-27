using Jackett.Models;

namespace Jackett.Utils
{
    public static class TorznabCapsUtil
    {
        public static TorznabCapabilities CreateDefaultTorznabTVCaps()
        {
            var caps = new TorznabCapabilities();
            caps.SearchAvailable = true;
            caps.TVSearchAvailable = true;
            caps.SupportsTVRageSearch = false;
            caps.Categories.AddRange(new[] { 
                new TorznabCategory { ID = "5000", Name = "TV" },
                new TorznabCategory { ID = "5030", Name = "TV/SD" },
                new TorznabCategory { ID = "5040", Name = "TV/HD" } 
            });
            return caps;
        }

    }
}
