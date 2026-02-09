var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "GMSD Windows Service API");

app.Run();
