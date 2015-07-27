using System.Net;

namespace Jackett.Utils.Clients
{
    public class WebClientStringResult
    {
        public HttpStatusCode Status { get; set; }
        public string Cookies { get; set; }
        public string Content { get; set; }
        public string RedirectingTo { get; set; }
    }
}
