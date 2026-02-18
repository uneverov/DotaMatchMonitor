using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DotaMatchMonitor.Helpers;

public static class JsonHelper
{

    public static async Task<JsonDocument> LoadHeroesFromOpenDotaAsync()
    {
        var url = Environment.GetEnvironmentVariable("OPENDOTA_HEROES_URL")
            ?? throw new Exception("OPENDOTA_HEROES_URL not set");

        using var http = new HttpClient();
        string json = await http.GetStringAsync(url);
        return JsonDocument.Parse(json);
    }

    public static string GetHeroName(JsonDocument heroDict, dynamic player_stats)
    {
        if (heroDict == null) throw new InvalidOperationException("SteamService not initialized");
        
        foreach (var hero in heroDict.RootElement.EnumerateArray())
        {
            if (hero.GetProperty("id").GetInt32() == player_stats.hero_id)
            {
                return hero.GetProperty("localized_name").GetString() ?? "Unknown";
            }
        }
        return "Unknown";
    }
}
