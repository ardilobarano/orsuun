using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;
using Orsuun.Server.Game;

var builder = WebApplication.CreateBuilder(args);

string connection = builder.Configuration.GetConnectionString("Game")
    ?? throw new InvalidOperationException("ConnectionStrings:Game is not configured.");
builder.Services.AddDbContext<GameDb>(o => o.UseNpgsql(connection));
builder.Services.AddSingleton<IRandom>(CryptoRandom.Instance);
builder.Services.AddSingleton<BellClock>();
builder.Services.AddScoped<GameService>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Abuse guard: 40 calls per 10 s per session (or per client IP before login). The game needs a few a minute.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        ctx.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip)
            ? RateLimitPartition.GetNoLimiter("loopback")      // local dev and the smoke test
            : RateLimitPartition.GetFixedWindowLimiter(
            ctx.Request.Headers["X-Session"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromSeconds(10), QueueLimit = 0 }));
});

// Behind Caddy the peer is the proxy; take the client IP from X-Forwarded-For. Safe because the server port is only
// reachable from the Docker network, and Caddy overwrites any X-Forwarded-For the client sends.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseRateLimiter();

using (IServiceScope scope = app.Services.CreateScope())
{
    // Schema via EF migrations (src/Orsuun.Server/Migrations). ORSUUN_RESET_DB=1 wipes the schema first; dev only.
    GameDb db = scope.ServiceProvider.GetRequiredService<GameDb>();
    if (app.Environment.IsDevelopment() && Environment.GetEnvironmentVariable("ORSUUN_RESET_DB") == "1")
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
    await db.Database.MigrateAsync();
    await GameService.SeedFortressesAsync(db, CancellationToken.None);
}

app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (GameException ex)
    {
        ctx.Response.StatusCode = ex.Code switch
        {
            "conflict" or "duplicate_request" => StatusCodes.Status409Conflict,
            "unauthorized" => StatusCodes.Status401Unauthorized,
            "siege_cooldown" => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest,
        };
        await ctx.Response.WriteAsJsonAsync(new ErrorDto(ex.Code, ex.Message));
    }
});

app.MapGet("/health", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow }));

app.MapPost("/v1/auth/guest", (GuestLoginRequest req, HttpContext http, GameService game, CancellationToken ct) =>
    game.GuestLoginAsync(req.DeviceToken, http.Connection.RemoteIpAddress?.ToString(), ct));

RouteGroupBuilder v1 = app.MapGroup("/v1").AddEndpointFilter(async (ctx, next) =>
{
    var game = ctx.HttpContext.RequestServices.GetRequiredService<GameService>();
    Account? account = await game.AuthenticateAsync(ctx.HttpContext.Request.Headers["X-Session"], ctx.HttpContext.RequestAborted);
    if (account == null) throw new GameException("unauthorized", "Missing or expired session.");
    ctx.HttpContext.Items["account"] = account;
    return await next(ctx);
});

static Account Me(HttpContext ctx) => (Account)ctx.Items["account"]!;

// /me and /heartbeat carry the Commander clocks; the mutating calls return state without them to stay light.
v1.MapGet("/me", (HttpContext ctx, GameService game, CancellationToken ct) => game.WithBossesAsync(Me(ctx), game.GetState(Me(ctx)), ct));
v1.MapPost("/heartbeat", async (HttpContext ctx, GameService game, CancellationToken ct) =>
{
    // The body is optional (older clients post "{}" or nothing): loop reports for active play.
    HeartbeatRequest? req = null;
    if (ctx.Request.ContentLength is > 0)
    {
        try { req = await ctx.Request.ReadFromJsonAsync<HeartbeatRequest>(ct); }
        catch (System.Text.Json.JsonException) { req = null; }    // a garbled report only costs the bonus
    }
    return await game.WithBossesAsync(Me(ctx), await game.HeartbeatAsync(Me(ctx), req, ct), ct);
});
v1.MapPost("/boss/fight", async (HttpContext ctx, BossFightRequest req, GameService game, CancellationToken ct) => await game.WithBossesAsync(Me(ctx), await game.FightBossAsync(Me(ctx), req, ct), ct));
v1.MapPost("/forge", (HttpContext ctx, ForgeRequest req, GameService game, CancellationToken ct) => game.ForgeAsync(Me(ctx), req, ct));
v1.MapPost("/turn", (HttpContext ctx, TurnRequest req, GameService game, CancellationToken ct) => game.TurnAsync(Me(ctx), req, ct));
v1.MapPost("/equip", (HttpContext ctx, EquipRequest req, GameService game, CancellationToken ct) => game.EquipAsync(Me(ctx), req, ct));
v1.MapPost("/socket/insert", (HttpContext ctx, SocketInsertRequest req, GameService game, CancellationToken ct) => game.SocketInsertAsync(Me(ctx), req, ct));
v1.MapPost("/socket/clear", (HttpContext ctx, SocketClearRequest req, GameService game, CancellationToken ct) => game.SocketClearAsync(Me(ctx), req, ct));
v1.MapPost("/park", (HttpContext ctx, ParkRequest req, GameService game, CancellationToken ct) => game.ParkAsync(Me(ctx), req, ct));
v1.MapPost("/class", (HttpContext ctx, ClassRequest req, GameService game, CancellationToken ct) => game.SetClassAsync(Me(ctx), req, ct));
v1.MapPost("/push", (HttpContext ctx, PushRequest req, GameService game, CancellationToken ct) => game.PushAsync(Me(ctx), req, ct));
v1.MapPost("/bounty/claim", (HttpContext ctx, ClaimBountyRequest req, GameService game, CancellationToken ct) => game.ClaimBountyAsync(Me(ctx), req, ct));
v1.MapPost("/shop/buy", (HttpContext ctx, ShopBuyRequest req, GameService game, CancellationToken ct) => game.BuyAsync(Me(ctx), req, ct));
v1.MapPost("/etch", (HttpContext ctx, EtchRequest req, GameService game, CancellationToken ct) => game.EtchAsync(Me(ctx), req, ct));
v1.MapPost("/pin", (HttpContext ctx, PinRequest req, GameService game, CancellationToken ct) => game.PinAsync(Me(ctx), req, ct));
v1.MapPost("/banner", (HttpContext ctx, BannerRequest req, GameService game, CancellationToken ct) => game.SwearAsync(Me(ctx), req, ct));
v1.MapGet("/war", (HttpContext ctx, GameService game, CancellationToken ct) => game.WarAsync(Me(ctx), ct));
v1.MapPost("/siege", (HttpContext ctx, SiegeRequest req, GameService game, CancellationToken ct) => game.SiegeAsync(Me(ctx), req, ct));
v1.MapPost("/client-log", async (HttpContext ctx, ClientLogRequest req, GameService game, CancellationToken ct) =>
{
    await game.LogClientErrorAsync(Me(ctx), req, ct);
    return Results.Ok(new { ok = true });
});
v1.MapDelete("/account", async (HttpContext ctx, GameService game, CancellationToken ct) =>
{
    await game.DeleteAccountAsync(Me(ctx), ct);
    return Results.Ok(new { deleted = true });
});

if (app.Environment.IsDevelopment())
{
    v1.MapPost("/dev/grant", (HttpContext ctx, GameService game, CancellationToken ct) => game.DevGrantAsync(Me(ctx), ct));
    v1.MapPost("/dev/bosses-up", async (HttpContext ctx, GameService game, CancellationToken ct) => await game.WithBossesAsync(Me(ctx), await game.DevBossesUpAsync(Me(ctx), ct), ct));
}

app.Run();
