namespace Jackett.Models
{
    public class ConfigurationDataBasicLogin : ConfigurationData
    {
        public StringItem Username { get; private set; }
        public StringItem Password { get; private set; }

        public ConfigurationDataBasicLogin()
        {
            Username = new StringItem { Name = "Username" };
            Password = new StringItem { Name = "Password" };
        }

        public override Item[] GetItems()
        {
            return new Item[] { Username, Password };
        }
    }
}
