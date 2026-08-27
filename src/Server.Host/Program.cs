using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ResellerSystem.Modules.Dashboard;
using ResellerSystem.Modules.Documents;
using ResellerSystem.Modules.Expenses;
using ResellerSystem.Modules.Import;
using ResellerSystem.Modules.Inventory;
using ResellerSystem.Modules.Reports;
using ResellerSystem.Modules.Sales;
using ResellerSystem.Server.Api.DependencyInjection;
using ResellerSystem.Server.Host.Startup;
using ResellerSystem.Server.Infrastructure.Configuration;
using ResellerSystem.Server.Infrastructure.Logging;
using ResellerSystem.Server.Modules.Abstractions;
using ResellerSystem.Web.Client.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Run as a Windows Service in production; behaves as a normal console app
// under `dotnet run` during development. See README "How to run".
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "ResellerSystem.Server";
});

var logsRoot = Path.Combine(AppContext.BaseDirectory, "logs");
Log.Logger = SerilogConfigurator.Build(builder.Configuration, logsRoot).CreateLogger();
builder.Host.UseSerilog();

// The statically-known list of business modules built into this server —
// Server.Host is the only project allowed to reference concrete module
// assemblies (see Server.Modules.Abstractions.IServerModuleCatalog).
// Order matters: Inventory first (Sales/Import/Reports all read its
// tables or, for Import, call its service directly — see
// Modules.Import.csproj comment for that one deliberate exception to the
// "modules don't depend on each other" rule). Reports/Import last since
// they consume the others.
var modules = new List<IResellerModule>
{
    new InventoryModule(),
    new SalesModule(),
    new ExpensesModule(),
    new DocumentsModule(),
    new ReportsModule(),
    new ImportModule(),
    new DashboardModule()
};

builder.Services.AddServerApiServices(builder.Configuration, modules);

var serverOptions = builder.Configuration.GetSection(ServerOptions.SectionName).Get<ServerOptions>() ?? new ServerOptions();
builder.WebHost.UseUrls(serverOptions.BindAddress);

// Web.Client (Blazor Server) — hosted inside this same Kestrel process/port
// rather than a separate site, so it's automatically same-origin with the
// API it calls (no CORS needed) and reachable through the same Cloudflare
// Tunnel used for remote access. ApiClient talks to this same process over
// loopback, exactly like any other API client (desktop app included).
var localApiBaseAddress = new Uri(serverOptions.BindAddress.Replace("0.0.0.0", "localhost"));
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<CircuitState>();
builder.Services.AddHttpClient<ApiClient>(client => client.BaseAddress = localApiBaseAddress);

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
try
{
    await StartupChecks.RunAsync(app.Services, startupLogger, modules);
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Startup checks failed. The server will not start.");
    throw;
}

app.UseServerApiPipeline();

// Web UI — mounted after the API pipeline; MapFallbackToPage only matches
// requests that don't hit an existing route, so /api/v1/* keeps working
// exactly as before this was added.
app.UseStaticFiles();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

startupLogger.LogInformation("ResellerSystem.Server.Host starting on {BindAddress}", serverOptions.BindAddress);
app.Run();
