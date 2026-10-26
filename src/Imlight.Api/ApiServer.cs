using Akka.Actor;
using Imlight.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Imlight.Api;

public class ApiServer
{
    private static WebApplication? s_app;

    private static readonly bool s_enabled =
        ConfigurationManager.Settings["API.Enabled"].AsBool(false);
    private static readonly bool s_enableWebUi =
        ConfigurationManager.Settings["API.EnableWebUI"].AsBool(false);
    private static readonly string s_host =
        ConfigurationManager.Settings["API.Host"].AsString("127.0.0.1");
    private static readonly ushort s_port =
        ConfigurationManager.Settings["API.Port"].AsUShort(12379);

    public static async Task StartAsync(ActorSystem actorSystem) {
        if (!s_enabled) {
            return;
        }

        var builder = WebApplication.CreateBuilder();

        // Suppress Microsoft console logging
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{s_host}:{s_port}");

        // Register ActorSystem for DI
        builder.Services.AddSingleton(actorSystem);

        builder.Services.AddControllers();
        builder.Services.AddSignalR();

        // Allow vite dev server for development
        builder.Services.AddCors(options => {
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials());
        });

        s_app = builder.Build();
        s_app.UseCors();
        s_app.MapControllers();

        // Health check endpoint.
        s_app.MapGet("/api/v1/health", () => Results.Ok(new {
            status = StatusCodes.Status200OK,
            timestamp = DateTimeOffset.UtcNow
        }));

        // Serve static files if wwwroot exists.
        var webRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
        if (s_enableWebUi && Directory.Exists(webRootPath)) {
            var fileProvider = new PhysicalFileProvider(webRootPath);
            s_app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            s_app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
            s_app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider });
        }

        await s_app.StartAsync();
        Logger.Information("Management API listening on http://{0}:{1} (WebUI: {2})",
            Logger.Args(s_host, s_port, s_enableWebUi));
    }

    public static async Task StopAsync() {
        if (s_app is not null) {
            await s_app.StopAsync();
            await s_app.DisposeAsync();
            s_app = null;
        }
    }
}
