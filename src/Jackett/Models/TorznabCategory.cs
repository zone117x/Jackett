using System.Collections.Generic;

namespace Jackett.Models
{
    public class TorznabCategory
    {
        public string ID { get; set; }
        public string Name { get; set; }

        public List<TorznabCategory> SubCategories { get; private set; }

        public TorznabCategory()
        {
            SubCategories = new List<TorznabCategory>();
        }
    }
}
