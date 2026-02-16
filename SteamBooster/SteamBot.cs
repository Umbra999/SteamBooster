using SteamBooster.Modules;
using SteamBooster.Wrappers;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using System.Net;
using System.Text.RegularExpressions;

namespace SteamBooster
{
    internal sealed class SteamBot
    {
        private static readonly Regex AppIdRegex = new(@"gamecards/(?<appid>\d+)(?:/|\?|&|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DropsRegex = new(@"(?<drops>\d+)\s*card\s*drops?\s*remaining", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PageRegex = new(@"[?&]p=(?<page>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public readonly SteamClient client;
        public readonly SteamUser steamUser;

        private AuthPollResult? authData;

        public readonly CustomObjects.SteamCredentials credentials;

        private readonly PlayHandler playHandler;
        private readonly CallbackManager manager;
        private readonly HttpClient httpClient;
        private readonly CookieContainer cookieContainer;
        private readonly TimeSpan farmCheckInterval;

        private CancellationTokenSource? farmLoopCts;

        private bool isLoggedOn;
        private bool isPlayingBlocked;
        private bool hasCommunityAuthCookies;
        private ulong steamId64;
        private string? lastFarmStatus;

        public SteamBot(CustomObjects.SteamCredentials cred)
        {
            credentials = cred;

            farmCheckInterval = TimeSpan.FromSeconds(Math.Clamp(credentials.FarmCheckIntervalSeconds, 15, 600));

            client = new SteamClient();
            manager = new CallbackManager(client);
            steamUser = client.GetHandler<SteamUser>() ?? throw new InvalidOperationException("SteamUser handler unavailable.");

            PlaySessionHandler playSessionHandler = new();
            playHandler = new PlayHandler(this);

            client.AddHandler(playSessionHandler);

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            manager.Subscribe<PlaySessionHandler.PlayingSessionStateCallback>(OnPlayingSession);

            cookieContainer = new CookieContainer();

            HttpClientHandler httpClientHandler = new()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = cookieContainer,
                UseCookies = true,
            };

            httpClient = new HttpClient(httpClientHandler);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SteamBooster/2.2 (+https://steamcommunity.com)");

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
            Logger.LogDebug($"Connected to Steam, logging in as {credentials.Username}");

            if (authData == null)
            {
                CredentialsAuthSession authSession = client.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
                {
                    Username = credentials.Username,
                    Password = credentials.Password,
                    IsPersistentSession = false,
                    ClientOSType = EOSType.Win11,
                    PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_SteamClient,
                    DeviceFriendlyName = credentials.DeviceName,
                    Authenticator = new UserConsoleAuthenticator(),
                    WebsiteID = "Client",
                }).GetAwaiter().GetResult();

                authData = authSession.PollingWaitForResultAsync().GetAwaiter().GetResult();
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

            isLoggedOn = false;
            hasCommunityAuthCookies = false;
            lastFarmStatus = null;
            StopFarmLoop();
            playHandler.StopPlaying();

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(10));
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

            isLoggedOn = true;
            isPlayingBlocked = false;
            steamId64 = callback.ClientSteamID?.ConvertToUInt64() ?? 0;
            lastFarmStatus = null;

            ConfigureCommunityAuthCookies();

            Logger.LogSuccess($"Logged in as {credentials.Username} ({steamId64})");
            StartFarmLoop();
        }

        private void ConfigureCommunityAuthCookies()
        {
            hasCommunityAuthCookies = false;

            if (steamId64 == 0 || authData == null || string.IsNullOrWhiteSpace(authData.AccessToken))
            {
                Logger.LogWarning("No access token available for Steam Community web session; badge scan may use non-owner view.");
                return;
            }

            try
            {
                string sessionId = Guid.NewGuid().ToString("N");
                string loginSecure = Uri.EscapeDataString($"{steamId64}||{authData.AccessToken}");

                cookieContainer.Add(new Uri("https://steamcommunity.com"), new Cookie("sessionid", sessionId, "/", ".steamcommunity.com"));
                cookieContainer.Add(new Uri("https://steamcommunity.com"), new Cookie("steamRememberLogin", "true", "/", ".steamcommunity.com"));
                cookieContainer.Add(new Uri("https://steamcommunity.com"), new Cookie("steamLoginSecure", loginSecure, "/", ".steamcommunity.com"));

                cookieContainer.Add(new Uri("https://store.steampowered.com"), new Cookie("sessionid", sessionId, "/", ".steampowered.com"));
                cookieContainer.Add(new Uri("https://store.steampowered.com"), new Cookie("steamRememberLogin", "true", "/", ".steampowered.com"));
                cookieContainer.Add(new Uri("https://store.steampowered.com"), new Cookie("steamLoginSecure", loginSecure, "/", ".steampowered.com"));

                hasCommunityAuthCookies = true;
                Logger.LogDebug("Steam Community auth cookies initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize Steam Community cookies: {ex.Message}");
            }
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Logger.LogWarning($"Logged off account: {callback.Result}");

            isLoggedOn = false;
            hasCommunityAuthCookies = false;
            lastFarmStatus = null;
            StopFarmLoop();
            playHandler.StopPlaying();

            if (callback.Result == EResult.LogonSessionReplaced || callback.Result == EResult.LoggedInElsewhere)
            {
                Logger.LogWarning("Logged on from a different place, reconnecting...");
                client.Disconnect();
            }
        }

        private void OnPlayingSession(PlaySessionHandler.PlayingSessionStateCallback callback)
        {
            isPlayingBlocked = callback.PlayingBlocked;

            if (isPlayingBlocked && playHandler.IsPlaying)
            {
                playHandler.StopPlaying();
                LogFarmStatus("Farm paused: account is currently in use.", Logger.LogWarning);
            }
            else if (!isPlayingBlocked)
            {
                Logger.LogDebug("Account is available, farm loop can resume");
            }
        }

        private void StartFarmLoop()
        {
            StopFarmLoop();

            farmLoopCts = new CancellationTokenSource();
            Task.Run(() => FarmLoopAsync(farmLoopCts.Token));
        }

        private void StopFarmLoop()
        {
            CancellationTokenSource? cts = farmLoopCts;
            farmLoopCts = null;

            if (cts == null)
            {
                return;
            }

            cts.Cancel();
            cts.Dispose();
        }

        private async Task FarmLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RunFarmIterationAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogFarmStatus($"Farm loop failed: {ex.Message}", Logger.LogError);
                }

                await Task.Delay(farmCheckInterval, cancellationToken);
            }
        }

        private async Task RunFarmIterationAsync(CancellationToken cancellationToken)
        {
            if (!isLoggedOn)
            {
                playHandler.StopPlaying();
                LogFarmStatus("Farm idle: not logged on.", Logger.LogDebug);
                return;
            }

            if (isPlayingBlocked)
            {
                playHandler.StopPlaying();
                LogFarmStatus("Farm paused: account is currently in use.", Logger.LogWarning);
                return;
            }

            List<uint> gamesToPlay = [];
            List<uint> dropGames = [];
            List<uint> manualGames = credentials.Games
                .Where(static game => game <= uint.MaxValue)
                .Select(static game => (uint)game)
                .Distinct()
                .ToList();

            string? diagnostic = null;

            if (credentials.AutoFarmCardDrops)
            {
                (Dictionary<uint, int> dropsByAppId, string? dropDiagnostic) = await GetGamesWithCardDropsAsync(cancellationToken);
                diagnostic = dropDiagnostic;

                if (dropsByAppId.Count > 0)
                {
                    dropGames = dropsByAppId
                        .OrderByDescending(static pair => pair.Value)
                        .ThenBy(static pair => pair.Key)
                        .Select(static pair => pair.Key)
                        .ToList();
                }
            }

            gamesToPlay.AddRange(dropGames);
            gamesToPlay.AddRange(manualGames);
            gamesToPlay = gamesToPlay.Distinct().ToList();

            if (gamesToPlay.Count > 0)
            {
                playHandler.SetGamesPlaying(gamesToPlay);

                if (dropGames.Count > 0 && manualGames.Count > 0)
                {
                    LogFarmStatus($"Playing {gamesToPlay.Count} games (drops + manual hours).", Logger.LogImportant);
                }
                else if (dropGames.Count > 0)
                {
                    LogFarmStatus($"Playing {dropGames.Count} drop games.", Logger.LogImportant);
                }
                else
                {
                    LogFarmStatus($"Playing {manualGames.Count} manual hour games.", Logger.LogImportant);
                }

                return;
            }

            playHandler.StopPlaying();

            if (!string.IsNullOrWhiteSpace(diagnostic))
            {
                LogFarmStatus(diagnostic, Logger.LogWarning);
            }
            else
            {
                LogFarmStatus("No card drops or manual games configured for this account.", Logger.LogSuccess);
            }
        }

        private async Task<(Dictionary<uint, int> DropsByAppId, string? Diagnostic)> GetGamesWithCardDropsAsync(CancellationToken cancellationToken)
        {
            Dictionary<uint, int> result = [];

            if (steamId64 == 0)
            {
                return (result, "Farm cannot scan badges: SteamID was not resolved.");
            }

            string firstPage = await FetchBadgePageAsync(1, cancellationToken);
            string? diagnostic = DiagnoseBadgePage(firstPage);

            ParseResult firstParse = ParseCardDrops(firstPage);
            MergeCardDrops(firstParse.DropsByAppId, result);

            int maxPage = GetMaxBadgePage(firstPage);

            for (int page = 2; page <= maxPage; page++)
            {
                string pageContent = await FetchBadgePageAsync(page, cancellationToken);
                ParseResult pageParse = ParseCardDrops(pageContent);
                MergeCardDrops(pageParse.DropsByAppId, result);
            }

            if (result.Count > 0)
            {
                return (result, null);
            }

            if (diagnostic != null)
            {
                return (result, diagnostic);
            }

            if (firstParse.DropPhraseCount > 0 && firstParse.LinkedDropCount == 0)
            {
                return (result, "Found drop text, but no matching app links were parsed. Steam page structure likely changed.");
            }

            if (firstParse.BadgeRowCount > 0 && firstParse.DropPhraseCount == 0)
            {
                return (result, hasCommunityAuthCookies
                    ? "Badges are visible, but no owner drop text is present in this response. Steam may be rejecting current auth cookies."
                    : "Badges are visible, but no owner drop text is present in this response. Steam may be returning a non-owner view.");
            }

            return (result, null);
        }

        private static string? DiagnoseBadgePage(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return "Could not read Steam badge page content.";
            }

            if (html.Contains("profile_private_info", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("This profile is private", StringComparison.OrdinalIgnoreCase))
            {
                return "Card-drop scan failed: profile badges are private to this request. Enable public badge visibility or use manual fallback.";
            }

            if (html.Contains("OpenID", StringComparison.OrdinalIgnoreCase) &&
                !html.Contains("badge_row", StringComparison.OrdinalIgnoreCase))
            {
                return "Card-drop scan hit a login/interstitial page, so no badge data was parsed.";
            }

            if (!html.Contains("badge_row", StringComparison.OrdinalIgnoreCase) &&
                !html.Contains("badges_sheet", StringComparison.OrdinalIgnoreCase) &&
                !html.Contains("badges_empty", StringComparison.OrdinalIgnoreCase))
            {
                return "Badge page format was not recognized, so no drops were parsed.";
            }

            return null;
        }

        private static void MergeCardDrops(Dictionary<uint, int> source, Dictionary<uint, int> target)
        {
            foreach ((uint appId, int drops) in source)
            {
                if (drops <= 0)
                {
                    continue;
                }

                target[appId] = drops;
            }
        }


        private async Task<string> FetchBadgePageAsync(int page, CancellationToken cancellationToken)
        {
            string url = hasCommunityAuthCookies
                ? $"https://steamcommunity.com/my/badges/?l=english&p={page}"
                : $"https://steamcommunity.com/profiles/{steamId64}/badges/?l=english&p={page}";

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        private static int GetMaxBadgePage(string pageHtml)
        {
            int maxPage = 1;

            foreach (Match match in PageRegex.Matches(pageHtml))
            {
                if (int.TryParse(match.Groups["page"].Value, out int parsedPage))
                {
                    maxPage = Math.Max(maxPage, parsedPage);
                }
            }

            return Math.Clamp(maxPage, 1, 300);
        }

        private static ParseResult ParseCardDrops(string pageHtml)
        {
            Dictionary<uint, int> dropsByAppId = [];

            if (string.IsNullOrWhiteSpace(pageHtml))
            {
                return new ParseResult(dropsByAppId, 0, 0, 0);
            }

            string decoded = WebUtility.HtmlDecode(pageHtml);
            int badgeRows = Regex.Matches(decoded, "badge_row", RegexOptions.IgnoreCase).Count;

            MatchCollection dropMatches = DropsRegex.Matches(decoded);
            int linkedDrops = 0;

            foreach (Match dropMatch in dropMatches)
            {
                if (!int.TryParse(dropMatch.Groups["drops"].Value, out int dropsLeft) || dropsLeft <= 0)
                {
                    continue;
                }

                int windowStart = Math.Max(0, dropMatch.Index - 8000);
                int windowLength = dropMatch.Index - windowStart;
                string prefixWindow = decoded.Substring(windowStart, windowLength);

                MatchCollection appMatches = AppIdRegex.Matches(prefixWindow);
                if (appMatches.Count == 0)
                {
                    continue;
                }

                Match appMatch = appMatches[^1];
                if (!uint.TryParse(appMatch.Groups["appid"].Value, out uint appId))
                {
                    continue;
                }

                dropsByAppId[appId] = dropsLeft;
                linkedDrops++;
            }

            return new ParseResult(dropsByAppId, badgeRows, dropMatches.Count, linkedDrops);
        }

        private void LogFarmStatus(string message, Action<object> logger)
        {
            if (string.Equals(lastFarmStatus, message, StringComparison.Ordinal))
            {
                return;
            }

            lastFarmStatus = message;
            logger(message);
        }

        private sealed record ParseResult(Dictionary<uint, int> DropsByAppId, int BadgeRowCount, int DropPhraseCount, int LinkedDropCount);
    }
}







