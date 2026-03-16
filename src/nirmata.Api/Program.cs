using FluentValidation.AspNetCore;
using nirmata.Api.HealthChecks;
using nirmata.Common.Exceptions;
using nirmata.Data.Context;
using nirmata.Data.Mapping;
using nirmata.Data.Repositories;
using nirmata.Services.Composition;
using nirmata.Services.Implementations;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
builder.Services.AddnirmataServices();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedResponse
});
app.MapControllers();

app.Run();

public partial class Program
{
}
