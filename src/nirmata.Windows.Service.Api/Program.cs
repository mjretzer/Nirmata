var builder = WebApplication.CreateBuilder(args);

// Configurable daemon API base URL; override via env var DaemonApi__BaseUrl
var daemonBaseUrl = builder.Configuration["DaemonApi:BaseUrl"] ?? "https://localhost:9000";
builder.WebHost.UseUrls(daemonBaseUrl);

// CORS: allowed origins configurable via Cors:AllowedOrigins (env var: Cors__AllowedOrigins__0, etc.)
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var allowedOrigins = builder.Environment.IsDevelopment()
    ? configuredOrigins
        .Concat(new[] { "https://localhost:8443" })
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray()
    : configuredOrigins;

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<nirmata.Windows.Service.Api.DaemonRuntimeState>();
builder.Services.AddSingleton<nirmata.Windows.Service.Api.DaemonCommandExecutor>();

// Capture host log output into the in-memory buffer served by GET /api/v1/logs.
// Registered as ILoggerProvider so DI injects the shared DaemonRuntimeState singleton.
builder.Logging.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider,
    nirmata.Windows.Service.Api.DaemonLogSink>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "nirmata Agent Manager API v1");
});

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();

// Expose Program to the test assembly via WebApplicationFactory<Program>
public partial class Program { }
