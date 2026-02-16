using HexedBooster.Wrappers;
using SteamKit2;
using SteamKit2.Internal;

namespace HexedBooster.Modules
{
    internal sealed class PlayHandler(SteamBot instance)
    {
        private readonly Lock gate = new();

        public bool IsPlaying { get; private set; }

        public void SetGamesPlaying(IEnumerable<uint> appIds)
        {
            List<uint> normalized = appIds.Distinct().ToList();

            lock (gate)
            {
                Logger.LogDebug($"Sending games played update: {normalized.Count} app(s)");
                SendGamesMessage(normalized);
                IsPlaying = normalized.Count > 0;
            }
        }

        public void StopPlaying()
        {
            lock (gate)
            {
                if (!IsPlaying)
                {
                    return;
                }

                Logger.LogDebug("Sending games played update: 0 app(s)");
                SendGamesMessage([]);
                IsPlaying = false;
            }
        }

        private void SendGamesMessage(List<uint> appIds)
        {
            ClientMsgProtobuf<CMsgClientGamesPlayed> gamesPlaying = new(EMsg.ClientGamesPlayed);

            foreach (uint appId in appIds)
            {
                gamesPlaying.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
                {
                    game_id = new GameID(appId),
                });
            }

            instance.client.Send(gamesPlaying);
        }
    }
}
