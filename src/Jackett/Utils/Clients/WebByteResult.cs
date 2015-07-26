using System.Net;

namespace Jackett.Utils.Clients
{
    public class WebClientByteResult
    {
        public HttpStatusCode Status { get; set; }
        public string Cookies { get; set; }
        public byte[] Content { get; set; }
        public string RedirectingTo { get; set; }
    }
}
