using System;
using System.Threading.Tasks;

using Serilog;

using DotaMatchMonitor.Services;

namespace DotaMatchMonitor.Helpers; 

public static class ConsoleHelper
{
    public static Task WaitForExitAsync(MatchChecker? checker)
    {
        var tcs = new TaskCompletionSource<object?>();

        Console.CancelKeyPress += (s, e) =>
        {
            Log.Warning("🛑 Получен сигнал остановки...");
            checker?.StopSequentialChecking();
            tcs.SetResult(null);
            e.Cancel = true;
        };

        return tcs.Task;
    }
}
