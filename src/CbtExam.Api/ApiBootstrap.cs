using CbtExam.Api.Hubs;
using CbtExam.Api.Services;
using CbtExam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;

namespace CbtExam.Api;

public static class ApiBootstrap
{
    public static async Task<WebApplication> BuildApp(string dbPath, string wwwrootPath, int port = 5000)
    {
        // Empty args + suppress default console/hosting noise inside WPF
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args        = [],
            WebRootPath = wwwrootPath,
            ContentRootPath = wwwrootPath
        });

        // Silence ASP.NET Core's own console logging (WPF has no console)
        builder.Logging.ClearProviders();

        builder.WebHost.UseKestrel(k =>
        {
            k.ListenAnyIP(port);
        });

        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dbDirectory))
            Directory.CreateDirectory(dbDirectory);

        builder.Services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite($"Data Source={dbPath}"));

        builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
            p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

        builder.Services.AddControllers();
        builder.Services.AddSignalR();
        builder.Services.AddScoped<SnapshotExportService>();

        var app = builder.Build();
        var adminKey = Environment.GetEnvironmentVariable("CBT_ADMIN_KEY") ?? "admin123";

        // Auto-migrate on startup — robust reset on schema mismatch
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            bool created = false;
            try
            {
                db.Database.EnsureCreated();
                // Verify schema is usable by probing a known table
                _ = db.Students.Count();
                created = true;
            }
            catch { }

            if (!created)
            {
                // Schema mismatch — close all connections, delete DB, recreate
                try { db.Database.CloseConnection(); } catch { }
                db.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { }

                // Fresh context after delete
                using var scope2 = app.Services.CreateScope();
                var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
                db2.Database.EnsureCreated();
                await DataSeeder.SeedAsync(db2);
                goto skipSeed;
            }
            await DataSeeder.SeedAsync(db);
            skipSeed:;
        }

        app.UseCors();
        app.Use(async (ctx, next) =>
        {
            if (!ctx.Request.Path.StartsWithSegments("/api/exams") &&
                !ctx.Request.Path.StartsWithSegments("/api/sessions") &&
                !ctx.Request.Path.StartsWithSegments("/api/students"))
            {
                await next();
                return;
            }

            if (ctx.Request.Headers.TryGetValue("X-Admin-Key", out var provided) &&
                string.Equals(provided.ToString(), adminKey, StringComparison.Ordinal))
            {
                await next();
                return;
            }

            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsync("Unauthorized admin request.");
        });
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();
        app.MapHub<ExamHub>("/hubs/exam");

        // SPA fallback — serve index.html for unknown routes
        app.MapFallbackToFile("index.html");

        return app;
    }
}
