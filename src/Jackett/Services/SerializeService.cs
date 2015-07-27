using Newtonsoft.Json;

namespace Jackett.Services
{
    public interface ISerializeService
    {
        string Serialise(object obj);
        T DeSerialise<T>(string json);
    }

    class SerializeService : ISerializeService
    {
        public string Serialise(object obj)
        {
            return JsonConvert.SerializeObject(obj,Formatting.Indented);
        }

        public T DeSerialise<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return default(T);
            }
        }
    }
}
