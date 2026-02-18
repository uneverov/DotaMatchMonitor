using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;

using Serilog;

namespace DotaMatchMonitor.Infrastructure;

public class TgClient(bool sendToTelegram)
{
    private readonly HttpClient _httpClient = new();
    private readonly string _telegram_api_url = Environment.GetEnvironmentVariable("TELEGRAM_API_URL")
            ?? throw new Exception("TELEGRAM_API_URL not set");
    private readonly string _tgBotToken = Environment.GetEnvironmentVariable("TG_BOT_TOKEN")
            ?? throw new Exception("TG_BOT_TOKEN not set");
    private readonly long _tgChatId = long.Parse(
            Environment.GetEnvironmentVariable("TG_CHAT_ID")
            ?? throw new Exception("TG_CHAT_ID not set")
        );
    private readonly bool _sendToTelegram = sendToTelegram;

    public async Task SendMessageAsync(string text)
    {
        try
        {
            string url = $"{_telegram_api_url}{_tgBotToken}/sendMessage";

            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("chat_id", _tgChatId.ToString()),
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("parse_mode", "HTML")
            ]);

            if (_sendToTelegram)
            {
                var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"Telegram error: {await response.Content.ReadAsStringAsync()}");
            }
            }
            else
            {
                Log.Warning("⚠️  Telegram сообщения отключены");
            }
            
        }
        catch (Exception ex)
        {
            Log.Error($"Telegram send error: {ex.Message}");
        }
    }

    public async Task NotifyTrackedPlayersAsync(DotaDatabase db)
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
    await SendMessageAsync(message);
}
}
