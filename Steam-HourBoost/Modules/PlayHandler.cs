using SteamKit2.Internal;
using SteamKit2;

namespace HexedBooster.Modules
{
    internal class PlayHandler(SteamBot instance)
    {
        public bool isPlaying;

        public void SetGamesPlaying(bool state)
        {
            if (state == isPlaying) return;

            isPlaying = state;

            var gamesPlaying = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);

            if (state)
            {
                foreach (ulong game in instance.credentials.Games)
                {
                    gamesPlaying.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
                    {
                        game_id = new GameID(game)
                    });
                }
            }

            instance.client.Send(gamesPlaying);
        }
    }
}
