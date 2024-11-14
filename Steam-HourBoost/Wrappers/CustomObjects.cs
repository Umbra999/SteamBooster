namespace HexedBooster.Wrappers
{
    internal class CustomObjects
    {
        public class SteamCredentials
        {
            public string username { get; set; }
            public string password { get; set; }
            public ulong[] Games { get; set; }
            public string DeviceName { get; set; }
        }
    }
}
