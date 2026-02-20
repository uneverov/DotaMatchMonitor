using System;

using SteamKit2.GC.Dota.Internal;

namespace DotaMatchMonitor.Helpers;

public static class FormaterHelper
{
    public static string FormatPlayerMessage(
        dynamic player_stats,
        string? heroName,
        string resultText,
        uint matchDuration,
        DateTime matchStart)
    {
        heroName ??= "Unknown";
        return
            $"<b>[Начало матча: {matchStart}] {player_stats.player_name}</b> - {resultText} \n\n" +
            $"🧙 <b>Герой:</b> <code>{heroName}</code>\n" +
            $"⚔️ <b>Урон:</b> <code>{player_stats.hero_damage}</code>\n" +
            $"🏰 <b>Урон по постройкам:</b> <code>{player_stats.tower_damage}</code>\n" +
            $"💊 <b>Лечение:</b> <code>{player_stats.hero_healing}</code>\n" +
            $"🗡️ <b>Убийства:</b> <code>{player_stats.kills}</code>\n" +
            $"💀 <b>Смерти:</b> <code>{player_stats.deaths}</code>\n" +
            $"🤝 <b>Помощи:</b> <code>{player_stats.assists}</code>\n" +
            $"💰 <b>GPM:</b> <code>{player_stats.gold_per_min}</code>\n" +
            $"📈 <b>XPM:</b> <code>{player_stats.xp_per_min}</code>\n" +
            $"🕒 <b>Длительность:</b> <code>{matchDuration / 60} мин</code>\n";
    }
    public static string GetMatchResultText(dynamic playerStats, bool direWon)
    {
        bool isDire = playerStats.team_number == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS;
        bool isWin = (isDire && direWon) || (!isDire && !direWon);
        return isWin ? "победил 🏆" : "проиграл 💀";
    }
}

