using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;
using Orsuun.Server.Game;

var builder = WebApplication.CreateBuilder(args);

string connection = builder.Configuration.GetConnectionString("Game")
    ?? throw new InvalidOperationException("ConnectionStrings:Game is not configured.");
builder.Services.AddDbContext<GameDb>(o => o.UseNpgsql(connection));
builder.Services.AddSingleton<IRandom>(CryptoRandom.Instance);
builder.Services.AddScoped<GameService>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    // Schema from the model. Replace with EF migrations before the first data-preserving deploy.
    // ORSUUN_RESET_DB=1 drops and recreates it; dev only, the model still changes between commits.
    GameDb db = scope.ServiceProvider.GetRequiredService<GameDb>();
    if (app.Environment.IsDevelopment() && Environment.GetEnvironmentVariable("ORSUUN_RESET_DB") == "1")
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;");
    await db.Database.EnsureCreatedAsync();
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
            _ => StatusCodes.Status400BadRequest,
        };
        await ctx.Response.WriteAsJsonAsync(new ErrorDto(ex.Code, ex.Message));
    }
});

app.MapGet("/health", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow }));

app.MapPost("/v1/auth/guest", (GuestLoginRequest req, GameService game, CancellationToken ct) => game.GuestLoginAsync(req.DeviceToken, ct));

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
v1.MapPost("/heartbeat", async (HttpContext ctx, GameService game, CancellationToken ct) => await game.WithBossesAsync(Me(ctx), await game.HeartbeatAsync(Me(ctx), ct), ct));
v1.MapPost("/boss/fight", async (HttpContext ctx, BossFightRequest req, GameService game, CancellationToken ct) => await game.WithBossesAsync(Me(ctx), await game.FightBossAsync(Me(ctx), req, ct), ct));
v1.MapPost("/forge", (HttpContext ctx, ForgeRequest req, GameService game, CancellationToken ct) => game.ForgeAsync(Me(ctx), req, ct));
v1.MapPost("/turn", (HttpContext ctx, TurnRequest req, GameService game, CancellationToken ct) => game.TurnAsync(Me(ctx), req, ct));
v1.MapPost("/equip", (HttpContext ctx, EquipRequest req, GameService game, CancellationToken ct) => game.EquipAsync(Me(ctx), req, ct));
v1.MapPost("/socket/insert", (HttpContext ctx, SocketInsertRequest req, GameService game, CancellationToken ct) => game.SocketInsertAsync(Me(ctx), req, ct));
v1.MapPost("/socket/clear", (HttpContext ctx, SocketClearRequest req, GameService game, CancellationToken ct) => game.SocketClearAsync(Me(ctx), req, ct));
v1.MapPost("/park", (HttpContext ctx, ParkRequest req, GameService game, CancellationToken ct) => game.ParkAsync(Me(ctx), req, ct));
v1.MapPost("/push", (HttpContext ctx, PushRequest req, GameService game, CancellationToken ct) => game.PushAsync(Me(ctx), req, ct));

if (app.Environment.IsDevelopment())
{
    v1.MapPost("/dev/grant", (HttpContext ctx, GameService game, CancellationToken ct) => game.DevGrantAsync(Me(ctx), ct));
    v1.MapPost("/dev/bosses-up", async (HttpContext ctx, GameService game, CancellationToken ct) => await game.WithBossesAsync(Me(ctx), await game.DevBossesUpAsync(Me(ctx), ct), ct));
}

app.Run();
