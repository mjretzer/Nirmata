using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using nirmata.Api.HealthChecks;
using nirmata.Common.Exceptions;
using nirmata.Data.Context;
using nirmata.Data.Mapping;
using nirmata.Data.Repositories;
using nirmata.Services.Composition;
using nirmata.Services.Implementations;
using nirmata.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        if (context.Exception is NotFoundException)
        {
            context.ProblemDetails.Status = StatusCodes.Status404NotFound;
            context.ProblemDetails.Title = "Not found";
        }
        else if (context.Exception is ForbiddenException)
        {
            context.ProblemDetails.Status = StatusCodes.Status403Forbidden;
            context.ProblemDetails.Title = "Forbidden";
        }
        else if (context.Exception is FileTooLargeException)
        {
            context.ProblemDetails.Status = StatusCodes.Status413PayloadTooLarge;
            context.ProblemDetails.Title = "File too large";
        }
        else if (context.Exception is ValidationFailedException)
        {
            context.ProblemDetails.Status = StatusCodes.Status400BadRequest;
            context.ProblemDetails.Title = "Validation failed";
        }
        else if (context.Exception is nirmataException)
        {
            context.ProblemDetails.Status = StatusCodes.Status500InternalServerError;
            context.ProblemDetails.Title = "Request failed";
        }
    };
});

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed"
            });
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<nirmata.Data.Dto.Validators.Projects.ProjectUpdateRequestValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDataProtection();
builder.Services.AddHttpClient();

// Configure Entity Framework with SQLite
builder.Services.AddDbContext<nirmataDbContext>(options =>
    options
        .UseLazyLoadingProxies()
        .UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Register repositories
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();

// Register services using composition root
builder.Services.AddnirmataServices(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("https://localhost:8443")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck("self", () => HealthCheckResult.Healthy("API is operational"));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception exception)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var title = "Request failed";

        if (exception is NotFoundException)
        {
            statusCode = StatusCodes.Status404NotFound;
            title = "Not found";
        }
        else if (exception is ForbiddenException)
        {
            statusCode = StatusCodes.Status403Forbidden;
            title = "Forbidden";
        }
        else if (exception is FileTooLargeException)
        {
            statusCode = StatusCodes.Status413PayloadTooLarge;
            title = "File too large";
        }
        else if (exception is ValidationFailedException)
        {
            statusCode = StatusCodes.Status400BadRequest;
            title = "Validation failed";
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
});

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "nirmata API v1");
});

app.UseHttpsRedirection();

app.UseCors("FrontendDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => string.Equals(registration.Name, "self", StringComparison.OrdinalIgnoreCase),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(report.Status.ToString());
    }
});

app.MapHealthChecks("/health/detailed", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedResponse
});
app.MapControllers();

// Ensure the SQLite database directory exists before opening the connection.
// SQLite creates the .db file automatically but cannot create missing parent directories.
var sqliteConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(sqliteConnectionString))
{
    var dataSource = new SqliteConnectionStringBuilder(sqliteConnectionString).DataSource;
    if (!string.IsNullOrEmpty(dataSource))
    {
        var dbDirectory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(dbDirectory))
            Directory.CreateDirectory(dbDirectory);
    }
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<nirmataDbContext>();

    try
    {
        var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();

        if (pending.Count > 0)
            app.Logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));
        else
            app.Logger.LogInformation("Database schema is up to date; no migrations to apply.");

        await dbContext.Database.MigrateAsync();

        if (pending.Count > 0)
            app.Logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migration failed during startup.");
        throw;
    }
}

app.Run();

public partial class Program
{
}
