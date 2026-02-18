using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Data.Sqlite;
using Serilog;

namespace DotaMatchMonitor.Infrastructure;

public class DotaDatabase : IDisposable

{
    private readonly SqliteConnection _connection;

    public DotaDatabase(string dbPath = "dota_stats.db")
    {
        var fullPath = Path.GetFullPath(dbPath);
        Log.Information($"📁 База данных: {fullPath}");

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        CreateTableIfNotExists();
    }

    private void CreateTableIfNotExists()
    {
        using var cmd = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS PlayerLastMatches (
                    AccountId INTEGER PRIMARY KEY,
                    LastMatchId INTEGER NOT NULL,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )", _connection);

        cmd.ExecuteNonQuery();
        Log.Information("✅ Таблица создана/проверена");
    }

    public void SaveLastMatch(uint accountId, ulong matchId)
    {
        using var cmd = new SqliteCommand(
            "UPDATE PlayerLastMatches SET LastMatchId = @mid WHERE AccountId = @aid",
            _connection);

        cmd.Parameters.AddWithValue("@aid", (long)accountId);
        cmd.Parameters.AddWithValue("@mid", (long)matchId);

        cmd.ExecuteNonQuery();
        Log.Information($"💾 Сохранено: {accountId} → {matchId}");
    }

    public ulong? GetLastMatch(uint accountId)
    {
        using var cmd = new SqliteCommand(
            "SELECT LastMatchId FROM PlayerLastMatches WHERE AccountId = @aid",
            _connection);

        cmd.Parameters.AddWithValue("@aid", (long)accountId);
        var result = cmd.ExecuteScalar();

        return result != null ? Convert.ToUInt64(result) : null;
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    public List<uint> GetTrackedPlayerIds()
    {
        var result = new List<uint>();

        using var cmd = new SqliteCommand(
            "SELECT AccountId FROM PlayerLastMatches",
            _connection);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((uint)reader.GetInt64(0));
        }

        return result;
    }
    public string? GetNickname(uint accountId)
    {
        using var cmd = new SqliteCommand(
            "SELECT Nickname FROM PlayerLastMatches WHERE AccountId = @aid",
            _connection);

        cmd.Parameters.AddWithValue("@aid", (long)accountId);

        var result = cmd.ExecuteScalar();

        if (result == null)
            return null;

        return result.ToString()?.Trim();
    }
}