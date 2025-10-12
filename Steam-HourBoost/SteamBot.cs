using HexedBooster.Modules;
using HexedBooster.Wrappers;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;

namespace HexedBooster
{
    internal class SteamBot
    {
        public readonly SteamClient client;
        public readonly SteamUser steamUser;
        private AuthPollResult authData;

        public readonly CustomObjects.SteamCredentials credentials;

        private readonly PlayHandler playHandler;

        public SteamBot(CustomObjects.SteamCredentials cred)
        {
            credentials = cred;

            client = new SteamClient();

            CallbackManager manager = new(client);

            steamUser = client.GetHandler<SteamUser>();

            var playSessionHandler1 = new PlaySessionHandler();
            playHandler = new PlayHandler(this);

            client.AddHandler(playSessionHandler1);

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            manager.Subscribe<PlaySessionHandler.PlayingSessionStateCallback>(OnPlayingSession);

            client.Connect();

            new Thread(() =>
            {
                while (true)
                {
                    manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
                }
            })
            { IsBackground = true }.Start();
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Logger.LogDebug($"Connected to Steam, logging in as {credentials.username}");

            if (authData == null)
            {
                CredentialsAuthSession authSession = client.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
                {
                    Username = credentials.username,
                    Password = credentials.password,
                    IsPersistentSession = false,
                    ClientOSType = EOSType.Win11,
                    PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_SteamClient,
                    DeviceFriendlyName = credentials.DeviceName,
                    Authenticator = new UserConsoleAuthenticator(),
                    WebsiteID = "Client",
                }).Result;

                authData = authSession.PollingWaitForResultAsync().Result;
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = authData.AccountName,
                AccessToken = authData.RefreshToken,
                ClientOSType = EOSType.Win11,
                AccountInstance = 1,
            });
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Logger.LogError("Disconnected from Steam");

            Task.Run(async () =>
            {
                await Task.Delay(600000);
                client.Connect();
            });
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Logger.LogError($"Unable to log into Steam: {callback.Result} / {callback.ExtendedResult}");
                return;
            }

            Logger.LogSuccess($"Logged in as {credentials.username}");;
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Logger.LogWarning($"Logged off Account: {callback.Result}");

            playHandler.isPlaying = false;

            if (callback.Result == EResult.LogonSessionReplaced || callback.Result == EResult.LoggedInElsewhere)
            {
                Logger.LogWarning("Logged on from a different place, reconnecting...");
                client.Disconnect();
            }
        }

        private void OnPlayingSession(PlaySessionHandler.PlayingSessionStateCallback callback)
        {
            if (callback.PlayingBlocked && playHandler.isPlaying)
            {
                playHandler.SetGamesPlaying(false);
                Logger.LogWarning("Account is in use, stopped playing");
            }
            else if (!callback.PlayingBlocked && !playHandler.isPlaying)
            {
                playHandler.SetGamesPlaying(true);
                Logger.LogWarning("Account is available, start playing");
            }
        }
    }
}
