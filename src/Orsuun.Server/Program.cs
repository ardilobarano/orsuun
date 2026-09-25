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
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ExternalAuth>();
// War nights are paired and settled, and fortress keeps move through their week, on this clock.
builder.Services.AddHostedService<WorldClock>();
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
    await GameService.BackfillNamesAsync(db, CancellationToken.None);   // characters made before names (25 Sep 2026)
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
            "siege_cooldown" or "chat_cooldown" or "login_wait" => StatusCodes.Status429TooManyRequests,
            "banned" => StatusCodes.Status403Forbidden,
            "admin_unauthorized" => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest,
        };
        await ctx.Response.WriteAsJsonAsync(new ErrorDto(ex.Code, ex.Message));
    }
});

app.MapGet("/health", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow }));

app.MapPost("/v1/auth/guest", (GuestLoginRequest req, HttpContext http, GameService game, CancellationToken ct) =>
    game.GuestLoginAsync(req.DeviceToken, http.Connection.RemoteIpAddress?.ToString(), ct, req.Lobby));

// Sign in with email and password: points this device at the account (no session needed; failed tries are limited).
app.MapPost("/v1/auth/login", (LoginRequest req, HttpContext http, GameService game, CancellationToken ct) =>
    game.LoginAsync(req, http.Connection.RemoteIpAddress?.ToString(), ct));

// Sign in with Google / Apple. The browser flow: /auth/{provider}/start (from the app) -> the provider -> the callback
// here -> an orsuun:// link with a one-time ticket -> /v1/auth/ticket from the device that started it.
app.MapGet("/v1/auth/providers", (ExternalAuth auth) => Results.Ok(new { providers = auth.Providers }));
app.MapGet("/auth/{provider}/start", (string provider, string? flow, ExternalAuth auth) =>
{
    ExternalAuth.Flow? f = auth.FlowById(flow);
    if (f == null || f.Provider != provider) return Results.Content(ExternalAuth.ReturnPage(null, "This sign-in link has expired. Start again in the game."), "text/html; charset=utf-8");
    return Results.Redirect(auth.AuthorizeUrl(f));
});
async Task<IResult> Finish(ExternalAuth auth, ExternalAuth.Flow? flow, Func<ExternalAuth.Flow, Task<ExternalIdentity>> verify)
{
    if (flow == null) return Results.Content(ExternalAuth.ReturnPage(null, "This sign-in has expired. Start again in the game."), "text/html; charset=utf-8");
    try
    {
        ExternalIdentity identity = await verify(flow);
        return Results.Content(ExternalAuth.ReturnPage(auth.IssueTicket(flow, identity), null), "text/html; charset=utf-8");
    }
    catch (GameException ex)
    {
        return Results.Content(ExternalAuth.ReturnPage(null, ex.Message), "text/html; charset=utf-8");
    }
}
app.MapGet("/auth/google/callback", (string? code, string? state, string? error, ExternalAuth auth, CancellationToken ct) =>
    Finish(auth, auth.TakeByState(state), async f =>
    {
        if (string.IsNullOrEmpty(code)) throw new GameException("cancelled", "Sign-in was cancelled.");
        string idToken = await auth.GoogleIdTokenAsync(code, f, ct);
        return await auth.VerifyAsync("google", idToken, f.Nonce, ct);
    }));
// Apple posts its answer as a form (response_mode=form_post).
app.MapPost("/auth/apple/callback", async (HttpContext http, ExternalAuth auth, CancellationToken ct) =>
{
    IFormCollection form = await http.Request.ReadFormAsync(ct);
    return await Finish(auth, auth.TakeByState(form["state"]), async f =>
    {
        string idToken = form["id_token"].ToString();
        if (idToken.Length == 0) throw new GameException("cancelled", "Sign-in was cancelled.");
        return await auth.VerifyAsync("apple", idToken, f.Nonce, ct);
    });
}).DisableAntiforgery();
if (app.Environment.IsDevelopment())
{
    // A stand-in provider for tests: the "sign-in page" is a link per test identity.
    // A fresh test login each time, or the shared Tester 1 (to try switching phones onto one hero).
    app.MapGet("/auth/dev/authorize", (string state) =>
    {
        string s = Uri.EscapeDataString(state), fresh = "tester-" + Guid.NewGuid().ToString("N")[..8];
        return Results.Content("<!doctype html><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            + "<style>body{background:#12111c;color:#f2e8d2;font:18px Georgia,serif;padding:12vh 24px;text-align:center}a{display:block;margin:18px auto;"
            + "max-width:320px;padding:14px;border:1px solid #c28f45;border-radius:8px;color:#ffd66b;text-decoration:none}</style>"
            + "<p>Orsuun test sign-in (playtest server only)</p>"
            + $"<a href=\"/auth/dev/callback?state={s}&sub={fresh}\">A new test login</a>"
            + $"<a href=\"/auth/dev/callback?state={s}&sub=tester-1&email=tester1@example.com\">Tester 1 (shared)</a>", "text/html; charset=utf-8");
    });
    app.MapGet("/auth/dev/callback", (string? state, string? sub, string? email, ExternalAuth auth, CancellationToken ct) =>
        Finish(auth, auth.TakeByState(state), f => auth.VerifyAsync("dev", $"dev|{sub}|{email}", null, ct)));
}
app.MapPost("/v1/auth/ticket", async (ExternalTicketRequest req, ExternalAuth auth, GameService game, CancellationToken ct) =>
{
    ExternalAuth.Ticket ticket = auth.Redeem(req.Ticket, req.DeviceToken) ?? throw new GameException("bad_ticket", "That sign-in expired or belongs to another device. Try again.");
    return await game.LinkOrLoginAsync(ticket.LoginId, ticket.DeviceToken, ticket.Identity, ct);
});

// The character screen: a session is enough, no character needs to be chosen (25 Sep 2026).
RouteGroupBuilder lobby = app.MapGroup("/v1/lobby").AddEndpointFilter(async (ctx, next) =>
{
    var game = ctx.HttpContext.RequestServices.GetRequiredService<GameService>();
    var found = await game.AuthenticateLoginAsync(ctx.HttpContext.Request.Headers["X-Session"], ctx.HttpContext.RequestAborted)
        ?? throw new GameException("unauthorized", "Missing or expired session.");
    ctx.HttpContext.Items["login"] = found.login;
    ctx.HttpContext.Items["device"] = found.device;
    return await next(ctx);
});
static Login MyLogin(HttpContext ctx) => (Login)ctx.Items["login"]!;
lobby.MapGet("", (HttpContext ctx, GameService game, CancellationToken ct) => game.LobbyAsync(MyLogin(ctx), "", ct));
lobby.MapPost("/create", (HttpContext ctx, CreateCharacterRequest req, GameService game, CancellationToken ct) => game.CreateCharacterAsync(MyLogin(ctx), req, ct));
lobby.MapPost("/select", async (HttpContext ctx, CharacterRequest req, GameService game, CancellationToken ct) =>
{
    StateDto state = await game.SelectCharacterAsync(MyLogin(ctx), (Device)ctx.Items["device"]!, req, ct);
    return state;
});
lobby.MapPost("/banner", (HttpContext ctx, BannerRequest req, GameService game, CancellationToken ct) => game.SwearLoginAsync(MyLogin(ctx), req, ct));
lobby.MapPost("/delete",(HttpContext ctx, CharacterRequest req, GameService game, CancellationToken ct) => game.DeleteCharacterAsync(MyLogin(ctx), req, ct));
lobby.MapPost("/signout", async (HttpContext ctx, GameService game, CancellationToken ct) =>
{
    await game.SignOutAsync(ctx.Request.Headers["X-Session"], ct);
    return Results.Ok(new { signedOut = true });
});
lobby.MapPost("/register", async (HttpContext ctx, RegisterRequest req, GameService game, CancellationToken ct) =>
{
    await game.RegisterAsync(MyLogin(ctx), req, ct);
    return await game.LobbyAsync(MyLogin(ctx), "Email saved: sign in with it on any device.", ct);
});
lobby.MapPost("/external/begin", (HttpContext ctx, ExternalBeginRequest req, ExternalAuth auth) =>
{
    ExternalAuth.Flow flow = auth.Begin(req.Provider, MyLogin(ctx).Id, ((Device)ctx.Items["device"]!).Token);
    return new ExternalBeginDto($"{auth.PublicUrl}/auth/{flow.Provider}/start?flow={flow.Id}");
});
lobby.MapPost("/external", async (HttpContext ctx, ExternalTokenRequest req, ExternalAuth auth, GameService game, CancellationToken ct) =>
{
    ExternalIdentity identity = await auth.VerifyAsync(req.Provider, req.IdToken, req.Nonce, ct);
    return await game.LinkOrLoginAsync(MyLogin(ctx).Id, ((Device)ctx.Items["device"]!).Token, identity, ct);
});

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
v1.MapGet("/guild", (HttpContext ctx, string? q, GameService game, CancellationToken ct) => game.GuildAsync(Me(ctx), q, ct));
// Guild war (asynchronous war nights) and the fortress keeps' bids and Sunday sieges.
v1.MapGet("/guild/war", (HttpContext ctx, GameService game, CancellationToken ct) => game.GuildWarAsync(Me(ctx), "", ct));
v1.MapPost("/guild/war/signup", (HttpContext ctx, GuildWarSignupRequest req, GameService game, CancellationToken ct) => game.GuildWarSignupAsync(Me(ctx), req, ct));
v1.MapPost("/guild/war/flag", (HttpContext ctx, GuildWarFlagRequest req, GameService game, CancellationToken ct) => game.GuildWarFlagAsync(Me(ctx), req, ct));
v1.MapPost("/guild/war/fight", (HttpContext ctx, GuildWarFightRequest req, GameService game, CancellationToken ct) => game.GuildWarFightAsync(Me(ctx), req, ct));
v1.MapGet("/pits", (HttpContext ctx, GameService game, CancellationToken ct) => game.PitsAsync(Me(ctx), "", ct));
v1.MapPost("/pits/refresh", (HttpContext ctx, GameService game, CancellationToken ct) => game.PitRefreshAsync(Me(ctx), ct));
v1.MapPost("/pits/fight", (HttpContext ctx, PitFightRequest req, GameService game, CancellationToken ct) => game.PitFightAsync(Me(ctx), req, ct));
v1.MapPost("/pits/shop", (HttpContext ctx, PitShopRequest req, GameService game, CancellationToken ct) => game.PitShopAsync(Me(ctx), req, ct));
v1.MapPost("/caravan/buy", (HttpContext ctx, CaravanBuyRequest req, GameService game, CancellationToken ct) => game.CaravanBuyAsync(Me(ctx), req, ct));
v1.MapPost("/caravan/amber", (HttpContext ctx, AmberPackRequest req, GameService game, CancellationToken ct) =>
    game.AmberPackAsync(Me(ctx), req, app.Environment.IsDevelopment(), ct));
v1.MapPost("/wardrobe/wear", (HttpContext ctx, WearRequest req, GameService game, CancellationToken ct) => game.WearAsync(Me(ctx), req, ct));
v1.MapPost("/trail/claim", (HttpContext ctx, TrailClaimRequest req, GameService game, CancellationToken ct) => game.TrailClaimAsync(Me(ctx), req, ct));
v1.MapPost("/trail/buy", (HttpContext ctx, TrailBuyRequest req, GameService game, CancellationToken ct) => game.TrailBuyAsync(Me(ctx), req, ct));
// Direct trade (Rules.DirectTrade): on the playtest server the level 30 and 72 hour rules are lifted so it can be tried.
v1.MapGet("/trade", (HttpContext ctx, GameService game, CancellationToken ct) => game.TradeAsync(Me(ctx), app.Environment.IsDevelopment(), ct));
v1.MapPost("/trade/invite", (HttpContext ctx, TradeInviteRequest req, GameService game, CancellationToken ct) =>
    game.TradeInviteAsync(Me(ctx), req, app.Environment.IsDevelopment(), ct));
v1.MapPost("/trade/accept", (HttpContext ctx, TradeRequest req, GameService game, CancellationToken ct) =>
    game.TradeAcceptAsync(Me(ctx), req, app.Environment.IsDevelopment(), ct));
v1.MapPost("/trade/cancel", (HttpContext ctx, TradeRequest req, GameService game, CancellationToken ct) =>
    game.TradeCancelAsync(Me(ctx), req, app.Environment.IsDevelopment(), ct));
v1.MapPost("/trade/offer", (HttpContext ctx, TradeOfferRequest req, GameService game, CancellationToken ct) =>
    game.TradeOfferAsync(Me(ctx), req, app.Environment.IsDevelopment(), ct));
v1.MapPost("/trade/press", (HttpContext ctx, TradeRequest req, GameService game, CancellationToken ct) =>
    game.TradePressAsync(Me(ctx), req, app.Environment.IsDevelopment(), ct));
v1.MapGet("/depot", (HttpContext ctx, GameService game, CancellationToken ct) => game.DepotAsync(Me(ctx), "", ct));
v1.MapPost("/depot/put", (HttpContext ctx, DepotRequest req, GameService game, CancellationToken ct) => game.DepotPutAsync(Me(ctx), req, ct));
v1.MapPost("/depot/take", (HttpContext ctx, DepotRequest req, GameService game, CancellationToken ct) => game.DepotTakeAsync(Me(ctx), req, ct));
v1.MapPost("/dungeon/enter", (HttpContext ctx, DungeonEnterRequest req, GameService game, CancellationToken ct) => game.EnterDungeonAsync(Me(ctx), req, ct));
v1.MapPost("/dungeon/smith", (HttpContext ctx, DungeonSmithRequest req, GameService game, CancellationToken ct) => game.DungeonSmithAsync(Me(ctx), req, ct));
v1.MapPost("/keep/bid", (HttpContext ctx, KeepBidRequest req, GameService game, CancellationToken ct) => game.KeepBidAsync(Me(ctx), req, ct));
v1.MapPost("/keep/fight", (HttpContext ctx, KeepFightRequest req, GameService game, CancellationToken ct) => game.KeepFightAsync(Me(ctx), req, ct));
v1.MapPost("/guild/create", (HttpContext ctx, GuildCreateRequest req, GameService game, CancellationToken ct) => game.CreateGuildAsync(Me(ctx), req, ct));
v1.MapPost("/guild/join", (HttpContext ctx, GuildJoinRequest req, GameService game, CancellationToken ct) => game.JoinGuildAsync(Me(ctx), req, ct));
v1.MapPost("/guild/leave", (HttpContext ctx, GuildLeaveRequest req, GameService game, CancellationToken ct) => game.LeaveGuildAsync(Me(ctx), req, ct));
v1.MapPost("/guild/kick", (HttpContext ctx, GuildMemberRequest req, GameService game, CancellationToken ct) => game.KickAsync(Me(ctx), req, ct));
v1.MapPost("/guild/rank", (HttpContext ctx, GuildMemberRequest req, GameService game, CancellationToken ct) => game.SetGuildRankAsync(Me(ctx), req, ct));
v1.MapPost("/guild/donate", (HttpContext ctx, GuildDonateRequest req, GameService game, CancellationToken ct) => game.DonateAsync(Me(ctx), req, ct));
v1.MapPost("/guild/skill", (HttpContext ctx, GuildSkillRequest req, GameService game, CancellationToken ct) => game.RaiseSkillAsync(Me(ctx), req, ct));
v1.MapPost("/guild/shop", (HttpContext ctx, GuildShopRequest req, GameService game, CancellationToken ct) => game.GuildBuyAsync(Me(ctx), req, ct));
v1.MapPost("/guild/settings", (HttpContext ctx, GuildSettingsRequest req, GameService game, CancellationToken ct) => game.GuildSettingsAsync(Me(ctx), req, ct));
v1.MapPost("/guild/answer", (HttpContext ctx, GuildAnswerRequest req, GameService game, CancellationToken ct) => game.AnswerRequestAsync(Me(ctx), req, ct));
v1.MapPost("/guild/invite", (HttpContext ctx, GuildInviteRequest req, GameService game, CancellationToken ct) => game.GuildInviteAsync(Me(ctx), req, ct));
v1.MapPost("/guild/invite/answer", (HttpContext ctx, GuildInviteAnswerRequest req, GameService game, CancellationToken ct) => game.AnswerGuildInviteAsync(Me(ctx), req, ct));
// Friends (25 Sep 2026): each hero's list, requests, and taking one off.
v1.MapGet("/friends", (HttpContext ctx, GameService game, CancellationToken ct) => game.FriendsAsync(Me(ctx), "", ct));
v1.MapPost("/friends/add", (HttpContext ctx, FriendAddRequest req, GameService game, CancellationToken ct) => game.AddFriendAsync(Me(ctx), req, ct));
v1.MapPost("/friends/answer", (HttpContext ctx, FriendAnswerRequest req, GameService game, CancellationToken ct) => game.AnswerFriendAsync(Me(ctx), req, ct));
v1.MapPost("/friends/remove", (HttpContext ctx, FriendRemoveRequest req, GameService game, CancellationToken ct) => game.RemoveFriendAsync(Me(ctx), req, ct));
v1.MapGet("/chat", (HttpContext ctx, string? channel, long? after, GameService game, CancellationToken ct) => game.ChatAsync(Me(ctx), channel, after ?? 0, ct));
v1.MapPost("/chat", (HttpContext ctx, ChatSayRequest req, GameService game, CancellationToken ct) => game.SayAsync(Me(ctx), req, ct));
v1.MapPost("/chat/report", (HttpContext ctx, ChatReportRequest req, GameService game, CancellationToken ct) => game.ReportAsync(Me(ctx), req, ct));
v1.MapPost("/chat/block", (HttpContext ctx, ChatBlockRequest req, GameService game, CancellationToken ct) => game.BlockAsync(Me(ctx), req, ct));
v1.MapGet("/market", (HttpContext ctx, EquipSlot? slot, string? sort, int? page, GameService game, CancellationToken ct) =>
    game.MarketAsync(Me(ctx), slot, sort, page ?? 0, ct));
v1.MapPost("/market/list", (HttpContext ctx, MarketListRequest req, GameService game, CancellationToken ct) => game.ListItemAsync(Me(ctx), req, ct));
v1.MapPost("/market/buy", (HttpContext ctx, MarketBuyRequest req, GameService game, CancellationToken ct) => game.BuyListingAsync(Me(ctx), req, ct));
v1.MapPost("/market/cancel", (HttpContext ctx, MarketBuyRequest req, GameService game, CancellationToken ct) => game.CancelListingAsync(Me(ctx), req, ct));
v1.MapPost("/auth/external/begin", async (HttpContext ctx, ExternalBeginRequest req, ExternalAuth auth, GameService game, CancellationToken ct) =>
{
    string device = await game.DeviceTokenForSessionAsync(ctx.Request.Headers["X-Session"], ct) ?? throw new GameException("unauthorized", "Sign in again.");
    ExternalAuth.Flow flow = auth.Begin(req.Provider, Me(ctx).LoginId, device);
    return new ExternalBeginDto($"{auth.PublicUrl}/auth/{flow.Provider}/start?flow={flow.Id}");
});
v1.MapPost("/auth/external", async (HttpContext ctx, ExternalTokenRequest req, ExternalAuth auth, GameService game, CancellationToken ct) =>
{
    string device = await game.DeviceTokenForSessionAsync(ctx.Request.Headers["X-Session"], ct) ?? throw new GameException("unauthorized", "Sign in again.");
    ExternalIdentity identity = await auth.VerifyAsync(req.Provider, req.IdToken, req.Nonce, ct);
    return await game.LinkOrLoginAsync(Me(ctx).LoginId, device, identity, ct);
});
v1.MapPost("/auth/register", (HttpContext ctx, RegisterRequest req, GameService game, CancellationToken ct) => game.RegisterAsync(Me(ctx), req, ct));
v1.MapPost("/auth/signout", async (HttpContext ctx, GameService game, CancellationToken ct) =>
{
    await game.SignOutAsync(ctx.Request.Headers["X-Session"], ct);
    return Results.Ok(new { signedOut = true });
});
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

// Moderation: the /admin page and its API. Moderators are accounts whose email is in Admin:Emails (comma separated;
// ADMIN_EMAILS in deploy/.env). With none configured the page still loads but nobody can sign in.
string[] admins = (app.Configuration["Admin:Emails"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(AccountRules.NormaliseEmail).ToArray();
static IResult Embedded(string name, string type)
{
    using Stream stream = typeof(GameService).Assembly.GetManifestResourceStream("Orsuun.Server.Admin." + name)!;
    using var reader = new StreamReader(stream);
    return Results.Text(reader.ReadToEnd(), type);
}
app.MapGet("/admin", (HttpContext ctx) =>
{
    // The page shows other players' words: no framing, no outside scripts, no inline script.
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    return Embedded("admin.html", "text/html; charset=utf-8");
});
app.MapGet("/admin/admin.js", () => Embedded("admin.js", "text/javascript; charset=utf-8"));
app.MapPost("/admin/api/login", (AdminLoginRequest req, HttpContext http, GameService game, CancellationToken ct) =>
    game.AdminLoginAsync(req, admins, http.Connection.RemoteIpAddress?.ToString(), ct));
RouteGroupBuilder mod = app.MapGroup("/admin/api").AddEndpointFilter(async (ctx, next) =>
{
    if (ctx.HttpContext.Request.Path.Value?.EndsWith("/login") == true) return await next(ctx);
    string? admin = GameService.AdminFor(ctx.HttpContext.Request.Headers["X-Admin"]);
    if (admin == null) throw new GameException("admin_unauthorized", "Sign in again.");
    ctx.HttpContext.Items["admin"] = admin;
    return await next(ctx);
});
static string Mod(HttpContext ctx) => (string)ctx.Items["admin"]!;
mod.MapGet("/overview", (GameService game, CancellationToken ct) => game.AdminOverviewAsync(ct));
mod.MapGet("/reports", (GameService game, CancellationToken ct) => game.AdminReportsAsync(ct));
mod.MapGet("/chat", (Guid? accountId, string? q, GameService game, CancellationToken ct) => game.AdminChatAsync(accountId, q, ct));
mod.MapPost("/lines/{id:long}", async (HttpContext ctx, long id, AdminLineRequest req, GameService game, CancellationToken ct) =>
{
    await game.AdminLineAsync(Mod(ctx), id, req, ct);
    return Results.Ok(new { ok = true });
});
mod.MapGet("/players", (string? q, GameService game, CancellationToken ct) => game.AdminPlayersAsync(q, ct));
mod.MapPost("/players/{id:guid}/mute", async (HttpContext ctx, Guid id, AdminMuteRequest req, GameService game, CancellationToken ct) =>
{
    await game.AdminMuteAsync(Mod(ctx), id, req, ct);
    return Results.Ok(new { ok = true });
});
mod.MapPost("/players/{id:guid}/ban", async (HttpContext ctx, Guid id, AdminBanRequest req, GameService game, CancellationToken ct) =>
{
    await game.AdminBanAsync(Mod(ctx), id, req, ct);
    return Results.Ok(new { ok = true });
});
mod.MapPost("/players/{id:guid}/unban", async (HttpContext ctx, Guid id, GameService game, CancellationToken ct) =>
{
    await game.AdminUnbanAsync(Mod(ctx), id, ct);
    return Results.Ok(new { ok = true });
});
mod.MapGet("/guilds", (string? q, GameService game, CancellationToken ct) => game.AdminGuildsAsync(q, ct));
mod.MapPost("/guilds/{id:guid}/rename", async (HttpContext ctx, Guid id, AdminRenameRequest req, GameService game, CancellationToken ct) =>
{
    await game.AdminRenameGuildAsync(Mod(ctx), id, req, ct);
    return Results.Ok(new { ok = true });
});
mod.MapPost("/guilds/{id:guid}/disband", async (HttpContext ctx, Guid id, GameService game, CancellationToken ct) =>
{
    await game.AdminDisbandGuildAsync(Mod(ctx), id, ct);
    return Results.Ok(new { ok = true });
});
mod.MapGet("/log", (GameService game, CancellationToken ct) => game.AdminLogAsync(ct));

if (app.Environment.IsDevelopment())
{
    v1.MapPost("/dev/grant", (HttpContext ctx, GameService game, CancellationToken ct) => game.DevGrantAsync(Me(ctx), ct));
    v1.MapPost("/dev/trail", (HttpContext ctx, int? xp, bool? lastSeason, GameService game, CancellationToken ct) =>
        game.DevTrailAsync(Me(ctx), xp ?? 0, lastSeason ?? false, ct));
    v1.MapPost("/dev/stage", (HttpContext ctx, int cleared, GameService game, CancellationToken ct) => game.DevStageAsync(Me(ctx), cleared, ct));
    v1.MapPost("/dev/gear", (HttpContext ctx, int level, int upgrade, GameService game, CancellationToken ct) => game.DevGearAsync(Me(ctx), level, upgrade, ct));
    v1.MapPost("/dev/war-night", (HttpContext ctx, int? minutes, GameService game, CancellationToken ct) => game.DevWarNightAsync(Me(ctx), minutes ?? 15, ct));
    v1.MapPost("/dev/war-end", (HttpContext ctx, GameService game, CancellationToken ct) => game.DevWarEndAsync(Me(ctx), ct));
    v1.MapPost("/dev/keep-siege", (HttpContext ctx, int? minutes, GameService game, CancellationToken ct) => game.DevKeepSiegeAsync(Me(ctx), minutes ?? 15, ct));
    v1.MapPost("/dev/keep-end", (HttpContext ctx, GameService game, CancellationToken ct) => game.DevKeepEndAsync(Me(ctx), ct));
    v1.MapPost("/dev/bosses-up", async (HttpContext ctx, GameService game, CancellationToken ct) => await game.WithBossesAsync(Me(ctx), await game.DevBossesUpAsync(Me(ctx), ct), ct));
}

app.Run();
