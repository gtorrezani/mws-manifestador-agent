using Mws.Manifestador.Agent.Application;
using Mws.Manifestador.Agent.Infrastructure;
using Mws.Manifestador.Agent.Sefaz;
using Mws.Manifestador.Agent.Worker.Services;
using Serilog;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

string programDataConfig = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "MWS Manifestador Agent",
    "appsettings.Local.json");
builder.Configuration.AddJsonFile(programDataConfig, optional: true, reloadOnChange: true);

builder.Services.AddWindowsService(static options =>
{
    options.ServiceName = "MWS Manifestador NF-e Agent";
});

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Services.AddSerilog();
builder.Services.Configure<LocalDiagnosticsOptions>(builder.Configuration.GetSection(LocalDiagnosticsOptions.SectionName));
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddSefazServices(builder.Configuration);

builder.Services.AddHostedService<AgentWorker>();
builder.Services.AddHostedService<LocalDiagnosticsService>();

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
