using System;

namespace Jackett.Models
{
    public class CachedResult
    {
        public ReleaseInfo Result
        {
            set; get;
        }

        public DateTime Created
        {
            set; get;
        }
    }
}
