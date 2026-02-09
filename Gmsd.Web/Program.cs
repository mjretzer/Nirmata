using Gmsd.Data.Context;
using Gmsd.Data.Mapping;
using Gmsd.Data.Repositories;
using Gmsd.Services.Composition;
using Gmsd.Web.Composition;
using Gmsd.Aos.Public;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages services
builder.Services.AddRazorPages();

// Add API controllers
builder.Services.AddControllers();

// Add session support for workspace tracking
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure Entity Framework with SQLite
builder.Services.AddDbContext<GmsdDbContext>(options =>
    options
        .UseLazyLoadingProxies()
        .UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=sqllitedb/gmsd.db"));

// Configure AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Register repositories
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();

// Register services using composition root
builder.Services.AddGmsdServices();

// Register GMSD Agents for direct in-process execution
builder.Services.AddGmsdAgents(builder.Configuration);

// Explicitly register SpecStore (workaround for DI resolution issue)
builder.Services.AddSingleton<SpecStore>(sp =>
{
    var workspace = sp.GetRequiredService<IWorkspace>();
    return SpecStore.FromWorkspace(workspace);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
