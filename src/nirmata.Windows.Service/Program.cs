using nirmata.Windows.Service;

var builder = Host.CreateApplicationBuilder(args);

// Run as a Windows Service under SCM; falls back to console/interactive lifetime when run directly.
// Detection is automatic via WindowsServiceHelpers.IsWindowsService().
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Nirmata Engine";
});

builder.Services.AddHostedService<Worker>();

// Optional: in-proc daemon API (alternative to companion-process; disabled by default).
// Enable via configuration: InProcDaemonApi:Enabled = true
// See nirmata.Windows.Service.Api/README.md for trade-offs vs companion-process.
if (builder.Configuration.GetValue<bool>("InProcDaemonApi:Enabled"))
{
    builder.Services.AddHostedService<InProcDaemonApiService>();
}

var host = builder.Build();
host.Run();
