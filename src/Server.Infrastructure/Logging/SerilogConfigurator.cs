using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace ResellerSystem.Server.Infrastructure.Logging;

/// <summary>
/// Central place that builds the Serilog pipeline. Splits output into
/// per-purpose rolling log files (application/error/database/update) as
/// required by the architecture, each with size + retained-file-count
/// limits so logs never grow unbounded.
/// </summary>
public static class SerilogConfigurator
{
    public static LoggerConfiguration Build(IConfiguration configuration, string logsRoot)
    {
        var config = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console();

        config = config.WriteTo.Logger(lc => lc
            .Filter.ByExcluding(e => e.Level >= LogEventLevel.Error)
            .WriteTo.File(
                Path.Combine(logsRoot, "application", "application-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true));

        config = config.WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
            .WriteTo.File(
                Path.Combine(logsRoot, "error", "error-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true));

        config = config.WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("LogCategory")
                                          && e.Properties["LogCategory"].ToString().Contains("Database"))
            .WriteTo.File(
                Path.Combine(logsRoot, "database", "database-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30));

        config = config.WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("LogCategory")
                                          && e.Properties["LogCategory"].ToString().Contains("Update"))
            .WriteTo.File(
                Path.Combine(logsRoot, "update", "update-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30));

        return config;
    }
}
