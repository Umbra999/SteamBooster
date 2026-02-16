using SteamBooster.Wrappers;
using System.Text.Json;

namespace SteamBooster
{
    internal static class Boot
    {
        private const string AccountsFileName = "Accounts.txt";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        private static readonly List<SteamBot> Bots = [];

        public static async Task Main()
        {
            Console.Title = "Steam - SteamBooster";

            CustomObjects.SteamCredentials[] accounts = await LoadAccountsAsync();

            if (accounts.Length == 0)
            {
                Logger.LogWarning("No accounts configured. Fill Accounts.txt and restart.");
                return;
            }

            foreach (CustomObjects.SteamCredentials credentials in accounts)
            {
                Bots.Add(new SteamBot(credentials));
            }

            await Task.Delay(Timeout.InfiniteTimeSpan);
        }

        private static async Task<CustomObjects.SteamCredentials[]> LoadAccountsAsync()
        {
            if (!File.Exists(AccountsFileName))
            {
                await WriteSampleAccountsAsync();
                Logger.LogImportant("Created Accounts.txt template. Fill it and restart.");
                return [];
            }

            string accountJson = await File.ReadAllTextAsync(AccountsFileName);

            if (string.IsNullOrWhiteSpace(accountJson))
            {
                Logger.LogError("Accounts.txt is empty.");
                return [];
            }

            CustomObjects.SteamCredentials[]? accounts = JsonSerializer.Deserialize<CustomObjects.SteamCredentials[]>(accountJson, JsonOptions);

            if (accounts == null)
            {
                Logger.LogError("Failed to parse Accounts.txt.");
                return [];
            }

            return accounts
                .Where(static account => !string.IsNullOrWhiteSpace(account.Username) && !string.IsNullOrWhiteSpace(account.Password))
                .ToArray();
        }

        private static Task WriteSampleAccountsAsync()
        {
            CustomObjects.SteamCredentials[] sample =
            [
                new CustomObjects.SteamCredentials
                {
                    Username = "your_steam_username",
                    Password = "your_steam_password",
                    DeviceName = "SteamBooster",
                    AutoFarmCardDrops = true,
                    FarmCheckIntervalSeconds = 180,
                    Games = [570],
                },
            ];

            string json = JsonSerializer.Serialize(sample, JsonOptions);
            return File.WriteAllTextAsync(AccountsFileName, json);
        }
    }
}

