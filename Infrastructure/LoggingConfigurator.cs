using System.IO;

using Microsoft.Extensions.Configuration;

using Serilog;

namespace DotaMatchMonitor.Infrastructure;

public static class LoggingConfigurator
{
    public static void Configure()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }
}