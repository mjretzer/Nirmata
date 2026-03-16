var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "nirmata Windows Service API");

app.Run();
