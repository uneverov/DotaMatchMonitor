using System;
using System.Threading.Tasks;

using DotNetEnv;
using Serilog;

using DotaMatchMonitor.Services;
using DotaMatchMonitor.Infrastructure;
using DotaMatchMonitor.Helpers;

namespace DotaMatchMonitor;

class App
{
    private static MatchChecker? checker;
    private static SteamService? steamService;
    private static DotaDatabase? db;
    static readonly string tgBotToken;
    static readonly long tgChatId;
    static readonly TgClient tg;
    
    static App()
    {
        Env.Load();
        tgBotToken = Environment.GetEnvironmentVariable("TG_BOT_TOKEN")
            ?? throw new Exception("TG_BOT_TOKEN not set");

        tgChatId = long.Parse(
            Environment.GetEnvironmentVariable("TG_CHAT_ID")
            ?? throw new Exception("TG_CHAT_ID not set")
        );
        tg = new TgClient(tgBotToken, tgChatId);
        
    }
    static async Task Main()
    {
        LoggingConfigurator.Configure();
        Log.Information("🚀 Запуск DotaMatchMonitor");
        db = new DotaDatabase();
        steamService = new SteamService();
        steamService.InitializeSteam();
        checker = new MatchChecker(steamService.GetGameCoordinator(), db);
        steamService.SetChecker(checker);

        await TgClient.NotifyTrackedPlayersAsync(db);
        await ConsoleHelper.WaitForExitAsync(checker);
    }
}