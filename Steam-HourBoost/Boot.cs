using HexedBooster.Wrappers;
using System.Text.Json;

namespace HexedBooster
{
    internal class Boot
    {
        private static readonly List<SteamBot> Bots = [];

        public static void Main()
        {
            Console.Title = "Steam - HexedBooster";

            if (!File.Exists("Accounts.txt"))
            {
                File.WriteAllText("Accounts.txt", JsonSerializer.Serialize(new[] { new CustomObjects.SteamCredentials() }, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            string AccountJson = File.ReadAllText("Accounts.txt");
            CustomObjects.SteamCredentials[] Accounts = JsonSerializer.Deserialize<CustomObjects.SteamCredentials[]>(AccountJson);

            foreach (CustomObjects.SteamCredentials credential in Accounts)
            {
                Bots.Add(new SteamBot(credential));
            }

            Thread.Sleep(-1);
        }
    }
}
