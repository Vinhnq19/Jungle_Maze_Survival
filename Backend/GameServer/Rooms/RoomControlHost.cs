using System.Security.Cryptography;
using System.Text;
using GameServer.Configuration;
using JungleBackend.Rooms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace GameServer.Rooms;

public static class RoomControlHost
{
    public static WebApplication? Start(RoomOptions options, RoomCommands commands)
    {
        if (!options.ControlEnabled) return null;
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = Array.Empty<string>() });
        builder.Logging.ClearProviders(); // Không log URL/header/request body chứa secret hay mã phòng.
        builder.WebHost.UseUrls($"http://{options.ControlBind}:{options.ControlPort}");
        builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = 4096);
        var app = builder.Build();
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(options.ControlToken));
        app.Use(async (context, next) =>
        {
            var supplied = context.Request.Headers["X-Room-Control-Key"].ToString();
            if (supplied.Length > 1024 || !CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            { context.Response.StatusCode = 401; return; }
            await next(context);
        });
        static IResult Respond(RoomResult result) => result.Body is null ? Results.StatusCode(result.Status)
            : Results.Json(result.Body, statusCode: result.Status);
        app.MapPost("/internal/rooms", async (CreateRoomCommand command, CancellationToken ct) =>
            Respond(await commands.Submit(rooms => rooms.Create(command), ct)));
        app.MapGet("/internal/rooms", async (string ownerId, CancellationToken ct) =>
            Respond(await commands.Submit(rooms => rooms.List(ownerId), ct)));
        app.MapDelete("/internal/rooms/{id:guid}", async (Guid id, string ownerId, CancellationToken ct) =>
            Respond(await commands.Submit(rooms => rooms.Delete(id, ownerId), ct)));
        app.StartAsync().GetAwaiter().GetResult();
        Console.WriteLine($"[room-control] enabled port={options.ControlPort}");
        return app;
    }
}
