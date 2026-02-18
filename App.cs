using System;
using System.Linq;
using System.Threading.Tasks;

using DotNetEnv;
using Serilog;

using DotaMatchMonitor.Services;
using DotaMatchMonitor.Infrastructure;
using DotaMatchMonitor.Helpers;


namespace DotaMatchMonitor;

class App
{
    static async Task Main(string[] args)
    {
        Env.Load();
        LoggingConfigurator.Configure();

        bool sendToTelegram = !args.Contains("-t");
        Log.Information("🚀 Запуск DotaMatchMonitor");

        var tg = new TgClient(sendToTelegram);
        var db = new DotaDatabase();
        var steamService = new SteamService();
        var checker = new MatchChecker(steamService.GC, db);
        var _ = new GCMessageHandler(steamService.SC, steamService.GC, tg, checker);

        await tg.NotifyTrackedPlayersAsync(db);
        await ConsoleHelper.WaitForExitAsync(checker);
    }
}
