using System.Text.Json.Serialization;

namespace SteamBooster.Wrappers
{
    internal static class CustomObjects
    {
        public sealed class SteamCredentials
        {
            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("password")]
            public string Password { get; set; } = string.Empty;

            [JsonPropertyName("games")]
            public ulong[] Games { get; set; } = [];

            [JsonPropertyName("deviceName")]
            public string DeviceName { get; set; } = "SteamBooster";

            [JsonPropertyName("autoFarmCardDrops")]
            public bool AutoFarmCardDrops { get; set; } = true;

            [JsonPropertyName("farmCheckIntervalSeconds")]
            public int FarmCheckIntervalSeconds { get; set; } = 90;
        }
    }
}

