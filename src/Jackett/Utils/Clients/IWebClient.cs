using System.Threading.Tasks;

namespace Jackett.Utils.Clients
{
    public interface IWebClient
    {
        Task<WebClientStringResult> GetString(WebRequest request);
        Task<WebClientByteResult> GetBytes(WebRequest request);
    }
}
