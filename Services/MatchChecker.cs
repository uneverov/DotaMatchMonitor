using System;
using System.Collections.Generic;
using Timer = System.Timers.Timer;

using Serilog;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;

using DotaMatchMonitor.Infrastructure;

namespace DotaMatchMonitor.Services;


public class MatchChecker
{
    const int DOTA_APP_ID = 570;
    private readonly List<uint> _trackedIds;
    public IReadOnlyList<uint> TrackedIds => _trackedIds;
    static DateTime _requestTime;
    public uint currentAccountId = 0;
    private int _currentPlayerIndex = 0;
    public bool waitingForResponse = false;
    private readonly DotaDatabase _db;
    private readonly SteamGameCoordinator _gc;
    private Timer? _checkTimer;
    public MatchChecker(SteamGameCoordinator gc, DotaDatabase db)
    {
        _db = db;
        _gc = gc;
        _trackedIds = _db.GetTrackedPlayerIds();
    }
    
    public void ProcessMatchHistory(SteamGameCoordinator.MessageCallback cb)
    {
        
        try
        {
            if (currentAccountId == 0)
            {
                Log.Warning("⚠️ Получен ответ, но неизвестно для какого аккаунта");
                waitingForResponse = false;
                return;
            }

            var response = new ClientGCMsgProtobuf<CMsgDOTAGetPlayerMatchHistoryResponse>(cb.Message);
            uint accountId = currentAccountId;

            Log.Information($"📜 Получены матчи для {accountId}: {response.Body.matches.Count}");

            if (response.Body.matches.Count > 0)
            {
                var firstMatch = response.Body.matches[0];
                ulong newMatchId = firstMatch.match_id;

                ulong? lastMatchId = _db.GetLastMatch(accountId);

                if (!lastMatchId.HasValue || lastMatchId.Value != newMatchId)
                {
                    Log.Information($"🆕 Обнаружен новый матч: {newMatchId}");
                    _db.SaveLastMatch(accountId, newMatchId);
                    RequestMatchDetails(newMatchId);
                }
                else
                {
                    Log.Information($"📊 Матч уже известен: {newMatchId}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"❌ Ошибка обработки истории матчей: {ex.Message}");
        }
    }
    public void StartSequentialChecking()
    {
        if (TrackedIds.Count == 0)
        {
            Log.Information("❌ Нет игроков для отслеживания");
            return;
        }

        _checkTimer = new Timer(60000);
        _checkTimer.Elapsed += (s, e) => CheckNextPlayer();
        _checkTimer.AutoReset = true;
        _checkTimer.Enabled = true;

        Log.Information($"⏰ Последовательная проверка запущена (каждые {_checkTimer.Interval / 1000} секунд)");

        CheckNextPlayer();
    }

    public void CheckNextPlayer()
    {
        if (TrackedIds.Count == 0)
        {
            Log.Information("❌ Нет игроков для отслеживания");
            return;
        }
        if (waitingForResponse)
        {
            if ((DateTime.Now - _requestTime).TotalSeconds > 10)
            {
                Log.Warning("⏰ Таймаут ответа GC");
                waitingForResponse = false;
                currentAccountId = 0;
            }
            else
            {
                return;
            }
        }

        // Берем следующего игрока по кругу
        if (_currentPlayerIndex >= TrackedIds.Count)
            _currentPlayerIndex = 0;

        currentAccountId = TrackedIds[_currentPlayerIndex];
        _currentPlayerIndex++;
        string? nickname = _db?.GetNickname(currentAccountId);
        string displayName = !string.IsNullOrEmpty(nickname) ? $"{currentAccountId}({nickname})" : currentAccountId.ToString();
        Log.Information($"🔍 Проверка игрока {displayName}({DateTime.Now:HH:mm:ss})");

        _requestTime = DateTime.Now;
        waitingForResponse = true;
        RequestLastMatch(currentAccountId);
    }
    public void RequestMatchDetails(ulong matchId)
    {
        try
        {
            if (_gc == null) return;

            var requestMatch = new ClientGCMsgProtobuf<CMsgGCMatchDetailsRequest>(
                (uint)EDOTAGCMsg.k_EMsgGCMatchDetailsRequest);
            requestMatch.Body.match_id = matchId;

            _gc.Send(requestMatch, DOTA_APP_ID);
            Log.Information($"📨 Запрошены детали матча {matchId}");
        }
        catch (Exception ex)
        {
            Log.Error($"❌ Ошибка запроса деталей: {ex.Message}");
        }
    }
    public void RequestLastMatch(uint accountId)
    {
        try
        {
            var req = new ClientGCMsgProtobuf<CMsgDOTAGetPlayerMatchHistory>(
                (uint)EDOTAGCMsg.k_EMsgDOTAGetPlayerMatchHistory);

            req.Body.account_id = accountId;
            req.Body.matches_requested = 1;

            _gc.Send(req, DOTA_APP_ID);
            Log.Information($"📨 Запрос отправлен для {accountId}");
        }
        catch (Exception ex)
        {
            Log.Error($"❌ Ошибка запроса для {accountId}: {ex.Message}");
            waitingForResponse = false;
            currentAccountId = 0;
        }
    }
    public void StopSequentialChecking()
    {
        if (_checkTimer != null)
        {
            _checkTimer.Stop();
            _checkTimer.Dispose();
            _checkTimer = null;
            Log.Information("🛑 Последовательная проверка остановлена");
            Log.Information("👋 Программа завершена");
        }
    }
    
}
