using SteamKit2.Internal;
using SteamKit2;

namespace HexedBooster.Modules
{
    internal class PlayHandler
    {
        private readonly SteamBot Bot;

        public bool isPlaying = false;

        public PlayHandler(SteamBot instance) 
        {
            Bot = instance;
        }

        public void SetGamesPlaying(bool state)
        {
            if (state == isPlaying) return;

            isPlaying = state;

            var gamesPlaying = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);

            if (state)
            {
                foreach (ulong game in Bot.credentials.Games)
                {
                    gamesPlaying.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
                    {
                        game_id = new GameID(game)
                    });
                }
            }

            Bot.client.Send(gamesPlaying);
        }
    }
}
