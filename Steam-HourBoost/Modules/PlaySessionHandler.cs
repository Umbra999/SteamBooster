using HexedBooster.Wrappers;
using SteamKit2;
using SteamKit2.Internal;

namespace HexedBooster.Modules
{
    internal sealed class PlaySessionHandler : ClientMsgHandler
    {
        internal sealed class PlayingSessionStateCallback : CallbackMsg
        {
            internal bool PlayingBlocked { get; }

            internal PlayingSessionStateCallback(JobID jobID, CMsgClientPlayingSessionState msg)
            {
                JobID = jobID;
                PlayingBlocked = msg.playing_blocked;
            }
        }

        public override void HandleMsg(IPacketMsg packetMsg)
        {
            if (packetMsg.MsgType != EMsg.ClientPlayingSessionState)
            {
                return;
            }

            HandlePlayingSessionState(packetMsg);
        }

        private void HandlePlayingSessionState(IPacketMsg packetMsg)
        {
            ClientMsgProtobuf<CMsgClientPlayingSessionState> response = new(packetMsg);

            if (response.Body == null)
            {
                Logger.LogError("Received empty playing session response.");
                return;
            }

            Client.PostCallback(new PlayingSessionStateCallback(packetMsg.TargetJobID, response.Body));
        }
    }
}
