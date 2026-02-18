using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;

using Serilog;

namespace DotaMatchMonitor.Infrastructure;

public class TgClient(string botToken, long chatId)
{
    private readonly string _botToken = botToken;
    private readonly long _chatId = chatId;
    private readonly HttpClient _httpClient = new();
    static readonly string telegram_api_url;

    static TgClient()
    {
        telegram_api_url = Environment.GetEnvironmentVariable("TELEGRAM_API_URL")
            ?? throw new Exception("TELEGRAM_API_URL not set");
    }

    public async Task SendMessageAsync(string text)
    {
        try
        {
            string url = $"{telegram_api_url}{_botToken}/sendMessage";

            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("chat_id", _chatId.ToString()),
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("parse_mode", "HTML")
            ]);

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"Telegram error: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Telegram send error: {ex.Message}");
        }
    }

    public static async Task NotifyTrackedPlayersAsync(DotaDatabase db)
    {
        var trackedIds = db.GetTrackedPlayerIds();

        if (trackedIds.Count == 0)
        {
            Log.Warning("⚠️ Нет отслеживаемых аккаунтов");
            return;
        }

        string message = "📊 Отслеживаю аккаунты:\n" +
            string.Join("\n", trackedIds.Select(id =>
            {
                var nick = db.GetNickname(id);
                return nick != null ? $"{nick}" : id.ToString();
            }));

        Log.Information($"Отправляем в TG сообщение:\n{message}");
        //await SendMessageAsync(message);
    }
}
