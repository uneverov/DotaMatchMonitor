using System;
using System.Linq;
using System.Threading.Tasks;

using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;

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

        var services = new ServiceCollection();

        services.AddSingleton(new TgClient(sendToTelegram));
        services.AddSingleton<DotaDatabase>();
        services.AddSingleton<SteamService>();
        services.AddSingleton<MatchChecker>();
        services.AddSingleton<GCMessageHandler>();

        var provider = services.BuildServiceProvider();

        var tg = provider.GetRequiredService<TgClient>();
        var db = provider.GetRequiredService<DotaDatabase>();
        var checker = provider.GetRequiredService<MatchChecker>();
        provider.GetRequiredService<SteamService>();
        provider.GetRequiredService<GCMessageHandler>();

        await tg.NotifyTrackedPlayersAsync(db);
        await ConsoleHelper.WaitForExitAsync(checker);
    }
}