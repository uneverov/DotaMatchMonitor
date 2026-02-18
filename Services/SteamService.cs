using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Serilog;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;

namespace DotaMatchMonitor.Services;

public class SteamService
{
    
    const int DOTA_APP_ID = 570;
    private SteamClient steamClient = null!;
    private SteamGameCoordinator gc = null!;
    bool running = true;
    string? steamLogin;
    string? steamPassword;
    static readonly List<uint> TrackedIds = [];
    private MatchChecker? _checker;
    private JsonDocument? heroDict;

    public SteamGameCoordinator GetGameCoordinator()
    {
        return gc;
    }
    public void SetChecker(MatchChecker checker)
    {
        _checker = checker;
    }
    
    public void InitializeSteam()
    {
        var url = Environment.GetEnvironmentVariable("OPENDOTA_HEROES_URL")
            ?? throw new Exception("OPENDOTA_HEROES_URL not set");

        using var http = new HttpClient();
        string json = http.GetStringAsync(url).GetAwaiter().GetResult();
        heroDict = JsonDocument.Parse(json);
        steamLogin = Environment.GetEnvironmentVariable("STEAM_LOGIN")
            ?? throw new Exception("STEAM_LOGIN not set");
        steamPassword = Environment.GetEnvironmentVariable("STEAM_PASSWORD")
            ?? throw new Exception("STEAM_PASSWORD not set");
        try
        {
            steamClient = new SteamClient();
            
            var manager = new CallbackManager(steamClient);
            var steamUser = steamClient.GetHandler<SteamUser>();
            gc = steamClient.GetHandler<SteamGameCoordinator>()
                ?? throw new InvalidOperationException("SteamGameCoordinator не найден");

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

            Log.Information("🔗 Подключение к Steam...");
            steamClient.Connect();

            // Фоновая обработка Steam колбэков
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
        catch (Exception ex)
        {
            Log.Error($"❌ Ошибка инициализации Steam: {ex.Message}");
            running = false;
        }
    }
    public void OnConnected(SteamClient.ConnectedCallback cb)
    {
        Log.Information("✅ Подключено к Steam");

        var steamUser = steamClient!.GetHandler<SteamUser>();
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

        steamClient!.Send(playGame);
        Thread.Sleep(3000);

        var clientHello = new ClientGCMsgProtobuf<CMsgClientHello>(
            (uint)EGCBaseClientMsg.k_EMsgGCClientHello);
        clientHello.Body.engine = ESourceEngine.k_ESE_Source2;

        gc!.Send(clientHello, DOTA_APP_ID);
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

            var response = new ClientGCMsgProtobuf<CMsgGCMatchDetailsResponse>(cb.Message);
            bool direWon = response.Body.match.match_outcome == EMatchOutcome.k_EMatchOutcome_DireVictory;
            for (int i = 0; i < response.Body.match.players.Count; i++)
            {
                if (TrackedIds.Contains(response.Body.match.players[i].account_id))
                {

                    var player_stats = response.Body.match.players[i];
                    string? heroName = "Unknown";
                    
                    if (heroDict == null) throw new InvalidOperationException("SteamService not initialized");

                    foreach (var hero in heroDict.RootElement.EnumerateArray())
                    {
                        if (hero.GetProperty("id").GetInt32() == player_stats.hero_id)
                        {
                            heroName = hero.GetProperty("localized_name").GetString();
                            break;
                        }
                    }
                    bool isDire = player_stats.team_number == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS;
                    bool isWin = (isDire && direWon) || (!isDire && !direWon);
                    string resultText = isWin ? "победил 🏆" : "проиграл 💀";
                    string message = $"<b>{player_stats.player_name}</b> - {resultText}\n\n" +
                    $"🧙 <b>Герой:</b> <code>{heroName}</code>\n" +
                    $"⚔️ <b>Урон:</b> <code>{player_stats.hero_damage}</code>\n" +
                    $"🏰 <b>Урон по постройкам:</b> <code>{player_stats.tower_damage}</code>\n" +
                    $"💊 <b>Лечение:</b> <code>{player_stats.hero_healing}</code>\n" +
                    $"🗡️ <b>Убийства:</b> <code>{player_stats.kills}</code>\n" +
                    $"💀 <b>Смерти:</b> <code>{player_stats.deaths}</code>\n" +
                    $"🤝 <b>Помощи:</b> <code>{player_stats.assists}</code>\n" +
                    $"💰 <b>GPM:</b> <code>{player_stats.gold_per_min}</code>\n" +
                    $"📈 <b>XPM:</b> <code>{player_stats.xp_per_min}</code>\n" +
                    $"🕒 <b>Длительность:</b> <code>{response.Body.match.duration / 60} мин</code>\n";
                    Log.Information($"Отправляем в tg сообщение:\n{message}");
                    //_ = tg?.SendMessageAsync(message);
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
        Log.Information("♻️ Переподключение через 5 секунд...");
        Thread.Sleep(5000);
        steamClient!.Connect();
    }

    public void OnLoggedOff(SteamUser.LoggedOffCallback cb)
    {
        Log.Information($"🚪 Logged off: {cb.Result}");
    }
}