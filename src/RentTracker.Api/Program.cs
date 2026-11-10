using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;
using System.Text.Json.Serialization;
using RentTracker.Api;
using RentTracker.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("RentDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Set the PostgreSQL connection string in the environment variable ConnectionStrings__RentDb.");
}

builder.Services.AddDbContext<RentDb>(options => options.UseNpgsql(connectionString));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Creates the tables on first run. Fine for a sample; use EF migrations for a real deployment.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<RentDb>().Database.EnsureCreated();
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "Server error" });
}));

app.UseCors();

// The built React app: ../../frontend/dist, same as the Express version.
var frontend = new PhysicalFileProvider(
    Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "frontend", "dist")));
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontend });
app.UseStaticFiles(new StaticFileOptions { FileProvider = frontend });

app.MapRentTrackerApi();

app.Run();
