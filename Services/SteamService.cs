using System;

using Serilog;
using SteamKit2;

namespace DotaMatchMonitor.Services;


public class SteamService
{
    private readonly SteamClient steamClient = null!;
    private readonly SteamGameCoordinator gc = null!;
    public SteamGameCoordinator GC => gc;
    public SteamClient SC => steamClient;
    
    public SteamService()
    {
        try
        {
            steamClient = new SteamClient();
            gc = steamClient.GetHandler<SteamGameCoordinator>()
                ?? throw new InvalidOperationException("SteamGameCoordinator не найден");
            Log.Information("🔗 Подключение к Steam...");
            steamClient.Connect();
        }
        catch (Exception ex)
        {
            Log.Error($"❌ Ошибка инициализации Steam: {ex.Message}");
        }
    }
}