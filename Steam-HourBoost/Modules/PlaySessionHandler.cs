using HexedBooster.Wrappers;
using SteamKit2;
using SteamKit2.Internal;

namespace HexedBooster.Modules
{
    internal class PlaySessionHandler : ClientMsgHandler
    {
        public PlaySessionHandler() { }

        internal sealed class PlayingSessionStateCallback : CallbackMsg
        {
            internal readonly bool PlayingBlocked;

            internal PlayingSessionStateCallback(JobID jobID, CMsgClientPlayingSessionState msg)
            {
                if (jobID == null || msg == null) Logger.LogError("PlayingSessionStateCallback received null parameters");

                JobID = jobID;
                PlayingBlocked = msg.playing_blocked;
            }
        }

        public override void HandleMsg(IPacketMsg packetMsg)
        {
            if (packetMsg == null) return;

            switch (packetMsg.MsgType)
            {
                case EMsg.ClientPlayingSessionState:
                    HandlePlayingSessionState(packetMsg);
                    break;
            }
        }

        private void HandlePlayingSessionState(IPacketMsg packetMsg)
        {
            if (packetMsg == null) return;

            ClientMsgProtobuf<CMsgClientPlayingSessionState> response = new(packetMsg);
            Client.PostCallback(new PlayingSessionStateCallback(packetMsg.TargetJobID, response.Body));
        }
    }
}
