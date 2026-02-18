using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

using Serilog;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;

using DotaMatchMonitor.Infrastructure;
using DotaMatchMonitor.Helpers;

namespace DotaMatchMonitor.Services;

public class GCMessageHandler

{
    string? steamLogin;
    string? steamPassword;
    bool running = true;
    const int DOTA_APP_ID = 570;
    private readonly SteamClient _steamClient;
    private JsonDocument? heroDict;
    private readonly SteamGameCoordinator _gc = null!;
    private readonly TgClient _tg = null!;
    private readonly MatchChecker _checker = null!;
    public GCMessageHandler(SteamClient steamClient, SteamGameCoordinator gc, TgClient? tg, MatchChecker? checker)
    {
        _steamClient = steamClient;
        _gc = gc ?? throw new ArgumentNullException(nameof(gc));
        _tg = tg ?? throw new ArgumentNullException(nameof(tg));
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
        var manager = new CallbackManager(_steamClient);
        var steamUser = _steamClient.GetHandler<SteamUser>();
        manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        manager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);
        manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

        _ = Task.Run(() =>
        {
            while (running && manager != null)
            {
                try
                {
                    manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    Log.Error($"⚠️ Ошибка в Steam колбэках: {ex.Message}");
                    Thread.Sleep(5000);
                }
            }
        });
    }
    public void OnConnected(SteamClient.ConnectedCallback _)
    {
        
        Log.Information("✅ Подключено к Steam");
        steamLogin = Environment.GetEnvironmentVariable("STEAM_LOGIN")
            ?? throw new Exception("STEAM_LOGIN not set");
        steamPassword = Environment.GetEnvironmentVariable("STEAM_PASSWORD")
            ?? throw new Exception("STEAM_PASSWORD not set");

        var steamUser = _steamClient.GetHandler<SteamUser>();
        steamUser!.LogOn(new SteamUser.LogOnDetails
        {
            Username = steamLogin,
            Password = steamPassword
        });
    }
    public void OnLoggedOn(SteamUser.LoggedOnCallback cb)
    {
        Log.Information($"🔐 Результат входа: {cb.Result}");

        if (cb.Result != EResult.OK)
        {
            Log.Error($"❌ Ошибка входа: {cb.Result}");
            running = false;
            return;
        }

        Log.Information("✅ Успешный вход в Steam!");

        var playGame = new ClientMsgProtobuf<SteamKit2.Internal.CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        playGame.Body.games_played.Add(new SteamKit2.Internal.CMsgClientGamesPlayed.GamePlayed
        {
            game_id = new GameID(DOTA_APP_ID),
        });

        _steamClient!.Send(playGame);
        Thread.Sleep(3000);

        var clientHello = new ClientGCMsgProtobuf<CMsgClientHello>(
            (uint)EGCBaseClientMsg.k_EMsgGCClientHello);
        clientHello.Body.engine = ESourceEngine.k_ESE_Source2;

        _gc.Send(clientHello, DOTA_APP_ID);
        Log.Information("🎮 Отправлен hello Game Coordinator");
    }
    public void OnGCMessage(SteamGameCoordinator.MessageCallback cb)
    {
        Log.Information($"📨 GC сообщение: {cb.EMsg}");

        if (cb.EMsg == 4004)
        {
            Log.Information("✅ Подключено к Dota 2 Game Coordinator");
            _checker?.StartSequentialChecking();
            return;
        }

        if (cb.EMsg == (uint)EDOTAGCMsg.k_EMsgDOTAGetPlayerMatchHistoryResponse)
        {
            _checker?.ProcessMatchHistory(cb);
            return;
        }

        if (cb.EMsg == (uint)EDOTAGCMsg.k_EMsgGCMatchDetailsResponse)
        {
            Log.Information("✅ Получены детали матча");
            heroDict = JsonHelper.LoadHeroesFromOpenDotaAsync().GetAwaiter().GetResult();
            var response = new ClientGCMsgProtobuf<CMsgGCMatchDetailsResponse>(cb.Message);
            bool direWon = response.Body.match.match_outcome == EMatchOutcome.k_EMatchOutcome_DireVictory;
            for (int i = 0; i < response.Body.match.players.Count; i++)
            {
                uint currentPlayerId = _checker.currentAccountId;
                if (response.Body.match.players[i].account_id == currentPlayerId)
                {
                    var player_stats = response.Body.match.players[i];
                    string heroName = JsonHelper.GetHeroName(heroDict, player_stats);
                    string resultText = FormaterHelper.GetMatchResultText(player_stats, direWon);
                    string message = FormaterHelper.FormatPlayerMessage(player_stats, heroName, resultText, response.Body.match.duration);
                    Log.Information($"Отправляем в tg сообщение:\n{message}");
                    _tg?.SendMessageAsync(message);
                    _checker.waitingForResponse = false;
                    _checker.currentAccountId = 0;
                    break;
                }
            }
            return;
        }
    }
    public void OnDisconnected(SteamClient.DisconnectedCallback cb)
    {
        Log.Warning("🔌 Отключено от Steam");
        if (!running) return;
        Log.Information("♻️  Переподключение через 5 секунд...");
        Thread.Sleep(5000);
        _steamClient.Connect();
    }

    public static void OnLoggedOff(SteamUser.LoggedOffCallback cb)
    {
        Log.Information($"🚪 Logged off: {cb.Result}");
    }

}
