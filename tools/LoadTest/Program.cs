// Orsuun load test (owner, 7 Oct 2026: "Load test before more testers"). Simulated players do what a phone does while it
// hunts, on the phone's own timers (Net/ServerLink.cs, ChatPanel, FieldFolk, PartyPanel): a heartbeat every 30 s, world chat
// every 6 s (every 2.5 s for the share with CHAT open), the field every 30 s, and in a party the party every 20 s and party
// chat every 10 s; now and then a line in world chat and a look at the state. Heartbeats carry loop reports, as a phone in
// active play sends, for the server to replay. Heroes are spread over the campaign's maps with a tester's bag (about a
// hundred pieces) and scroll stacks, and a share hunt in parties of four. It prints each endpoint's calls, errors and
// latency (median, 95th and 99th percentile). tools/loadtest.sh runs it with the server's CPU measured.
// Run it against a local copy of the server, never the playtest server (it makes guest heroes and uses the dev tools):
//   dotnet run -c Release --project tools/LoadTest -- --url http://localhost:5090 --heroes 100 --minutes 3
// Options: --heroes N (100), --minutes M (3), --ramp S (seconds to bring every hero in, 30), --party P (share in parties, 0.4),
//   --chatopen C (share with CHAT open, 0.1), --bag B (pieces in each hero's bag, 96), --dormant D (heroes made and left
//   idle, as signed-up testers who are away, 0), --only a,b (only these endpoints, by the report's names: one's cost),
//   --noloops (heartbeats without loop reports, as a phone left alone sends), --keep (leave the heroes; they are deleted
//   at the end otherwise).
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

string url = Arg("--url", "http://localhost:5090");
int heroes = int.Parse(Arg("--heroes", "100"), CultureInfo.InvariantCulture);
double minutes = double.Parse(Arg("--minutes", "3"), CultureInfo.InvariantCulture);
double ramp = double.Parse(Arg("--ramp", "30"), CultureInfo.InvariantCulture);
double partyShare = double.Parse(Arg("--party", "0.4"), CultureInfo.InvariantCulture);
double chatOpen = double.Parse(Arg("--chatopen", "0.1"), CultureInfo.InvariantCulture);
bool keep = args.Contains("--keep");
bool loops = !args.Contains("--noloops");
int bag = int.Parse(Arg("--bag", "96"), CultureInfo.InvariantCulture);
string[] only = Arg("--only", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
bool On(string name) => only.Length == 0 || only.Contains(name);

var http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = 512, PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
{
    BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(30),
};
var stats = new ConcurrentDictionary<string, ConcurrentBag<double>>();
var errors = new ConcurrentDictionary<string, int>();
var rng = new Random(7);

async Task<JsonNode?> Call(string name, HttpMethod method, string path, string? session, object? body = null)
{
    var request = new HttpRequestMessage(method, path);
    if (session != null) request.Headers.Add("X-Session", session);
    if (body != null) request.Content = JsonContent.Create(body);
    var sw = Stopwatch.StartNew();
    try
    {
        using HttpResponseMessage response = await http.SendAsync(request);
        string text = await response.Content.ReadAsStringAsync();
        stats.GetOrAdd(name, _ => new ConcurrentBag<double>()).Add(sw.Elapsed.TotalMilliseconds);
        if (!response.IsSuccessStatusCode)
        {
            string code = text.Length > 0 && text[0] == '{' ? JsonNode.Parse(text)?["code"]?.ToString() ?? "" : "";
            errors.AddOrUpdate($"{name} {(int)response.StatusCode} {code}", 1, (_, n) => n + 1);
            return null;
        }
        return text.Length > 0 ? JsonNode.Parse(text) : null;
    }
    catch (Exception e)
    {
        errors.AddOrUpdate(name + " " + e.GetType().Name, 1, (_, n) => n + 1);
        return null;
    }
}

string Rid() => Guid.NewGuid().ToString("N");
object FakeLoop(int n) => new { loop = n, ticks = 60 * 20, potions = 0, autoCast = new[] { true, true, true, true, true }, casts = Array.Empty<object>() };
// Setup steps are named by their path (without the query) so a failing one shows which.
string Step(string path) => "setup " + path.Split('?')[0];

// ---- Setup: heroes on every map, some in parties of four (friends first, as the server asks) ----
Console.WriteLine($"Setting up {heroes} heroes against {url} ...");
var setup = Stopwatch.StartNew();
var sessions = new string[heroes];
var ids = new string[heroes];
var party = new bool[heroes];
await Parallel.ForEachAsync(Enumerable.Range(0, heroes), new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (i, _) =>
{
    JsonNode? auth = await Call(Step("/v1/auth/guest"), HttpMethod.Post, "/v1/auth/guest", null, new { deviceToken = "load-" + Rid() });
    string session = auth?["sessionToken"]?.GetValue<string>() ?? "";
    sessions[i] = session;
    ids[i] = (await Call(Step("/v1/me"), HttpMethod.Get, "/v1/me", session))?["accountId"]?.GetValue<string>() ?? "";
    int stage = 1 + (i * 7919) % 120;   // spread over the twelve maps
    await Call(Step("/v1/dev/stage"), HttpMethod.Post, $"/v1/dev/stage?cleared={stage}", session, new { });
    await Call(Step("/v1/dev/level"), HttpMethod.Post, $"/v1/dev/level?level={Math.Min(105, stage)}", session, new { });
    // A bag like a real tester's (about a hundred pieces: each dev/gear puts the worn set in the bag and wears eight new
    // ones) and scroll stacks, then gear of the hero's level at +6, so its loops end the way a real hunter's do.
    for (int b = 0; b < bag / 8; b++)
        await Call(Step("/v1/dev/gear"), HttpMethod.Post, $"/v1/dev/gear?level={1 + b * 7 % Math.Min(105, stage)}&upgrade=0", session, new { });
    await Call(Step("/v1/dev/grant"), HttpMethod.Post, "/v1/dev/grant", session, new { });
    await Call(Step("/v1/dev/gear"), HttpMethod.Post, $"/v1/dev/gear?level={Math.Min(105, stage)}&upgrade=6", session, new { });
    await Call(Step("/v1/park"), HttpMethod.Post, "/v1/park", session, new { stage });
});
// Heroes who signed up once and never came back: they sit in the tables the polls read.
int dormant = int.Parse(Arg("--dormant", "0"), CultureInfo.InvariantCulture);
var idle = new string[dormant];
await Parallel.ForEachAsync(Enumerable.Range(0, dormant), new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (i, _) =>
    idle[i] = (await Call(Step("/v1/auth/guest"), HttpMethod.Post, "/v1/auth/guest", null, new { deviceToken = "load-" + Rid() }))?["sessionToken"]?.GetValue<string>() ?? "");
int groups = (int)(heroes * partyShare) / 4;
for (int g = 0; g < groups; g++)
{
    int lead = g * 4;
    // A party hunts one map: the members join the leader's stage.
    JsonNode? me = await Call(Step("/v1/me"), HttpMethod.Get, "/v1/me", sessions[lead]);
    int stage = me?["parkedStage"]?.GetValue<int>() ?? 1;
    party[lead] = true;
    for (int m = lead + 1; m < lead + 4; m++)
    {
        await Call(Step("/v1/friends/add"), HttpMethod.Post, "/v1/friends/add", sessions[lead], new { accountId = ids[m] });
        await Call(Step("/v1/friends/answer"), HttpMethod.Post, "/v1/friends/answer", sessions[m], new { accountId = ids[lead], accept = true });
        await Call(Step("/v1/dev/stage"), HttpMethod.Post, $"/v1/dev/stage?cleared={stage}", sessions[m], new { });
        await Call(Step("/v1/park"), HttpMethod.Post, "/v1/park", sessions[m], new { stage });
        await Call(Step("/v1/party/invite"), HttpMethod.Post, "/v1/party/invite", sessions[lead], new { accountId = ids[m] });
        await Call(Step("/v1/party/answer"), HttpMethod.Post, "/v1/party/answer", sessions[m], new { accept = true });
        party[m] = true;
    }
}
Console.WriteLine($"Setup took {setup.Elapsed.TotalSeconds:0}s ({groups} parties of four). Running for {minutes} minutes ...");
foreach ((string name, int n) in errors) Console.WriteLine($"setup error {name}: {n}");
stats.Clear();
errors.Clear();

// ---- The run: every hero on the phone's timers ----
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(ramp));
var run = Stopwatch.StartNew();
var tasks = new List<Task>();
for (int i = 0; i < heroes; i++)
{
    int hero = i;
    double startAt = ramp * i / Math.Max(1, heroes);
    bool open = rng.NextDouble() < chatOpen;
    tasks.Add(Task.Run(async () =>
    {
        var local = new Random(hero);
        try { await Task.Delay(TimeSpan.FromSeconds(startAt), cts.Token); } catch (TaskCanceledException) { return; }
        string s = sessions[hero];
        double now() => run.Elapsed.TotalSeconds;
        double next(double every) => now() + every * (0.9 + 0.2 * local.NextDouble());
        double heartbeat = now(), chat = now() + local.NextDouble() * 6, field = now() + local.NextDouble() * 30,
            partyAt = now() + local.NextDouble() * 20, partyChat = now() + local.NextDouble() * 10,
            say = now() + 60 + local.NextDouble() * 240, state = now() + local.NextDouble() * 120;
        long worldAfter = 0, partyAfter = 0;
        int loop = -1;
        while (!cts.IsCancellationRequested)
        {
            double t = now();
            if (t >= heartbeat && On("heartbeat"))
            {
                heartbeat = next(30);
                // A phone in active play reports the loops it finished; the server replays each twice (the player's
                // casts and plain auto-cast). Two made-up loops of up to a minute cost about as much: each is replayed
                // once, to its real end, and does not match (so it earns nothing).
                object body = loops && loop >= 0
                    ? new { loops = new[] { FakeLoop(loop), FakeLoop(loop + 1) } }
                    : new { };
                JsonNode? beat = await Call("heartbeat", HttpMethod.Post, "/v1/heartbeat", s, body);
                loop = beat?["lane"]?["loop"]?.GetValue<int>() ?? loop;
            }
            if (t >= chat && On("chat world"))
            {
                chat = next(open ? 2.5 : 6);
                JsonNode? c = await Call("chat world", HttpMethod.Get, $"/v1/chat?channel=world&after={worldAfter}", s);
                worldAfter = c?["latest"]?.GetValue<long>() ?? worldAfter;
            }
            if (t >= field && On("field")) { field = next(30); await Call("field", HttpMethod.Get, "/v1/field", s); }
            if (party[hero] && t >= partyAt && On("party")) { partyAt = next(20); await Call("party", HttpMethod.Get, "/v1/party", s); }
            if (party[hero] && t >= partyChat && On("chat party"))
            {
                partyChat = next(10);
                JsonNode? c = await Call("chat party", HttpMethod.Get, $"/v1/chat?channel=party&after={partyAfter}", s);
                partyAfter = c?["latest"]?.GetValue<long>() ?? partyAfter;
            }
            if (t >= say && On("chat say"))
            {
                say = next(300);
                await Call("chat say", HttpMethod.Post, "/v1/chat", s, new { channel = "world", text = "load test line " + local.Next(1000), after = worldAfter });
            }
            if (t >= state && On("me")) { state = next(120); await Call("me", HttpMethod.Get, "/v1/me", s); }
            try { await Task.Delay(200, cts.Token); } catch (TaskCanceledException) { break; }
        }
    }));
}
await Task.WhenAll(tasks);
double seconds = run.Elapsed.TotalSeconds - ramp / 2;

// ---- The report ----
Console.WriteLine();
Console.WriteLine($"{heroes} heroes, {seconds:0} s (after half the ramp):");
Console.WriteLine($"{"endpoint",-14} {"calls",7} {"per s",7} {"p50 ms",8} {"p95 ms",8} {"p99 ms",8} {"max ms",8}");
long total = 0;
foreach ((string name, ConcurrentBag<double> took) in stats.OrderByDescending(kv => kv.Value.Count))
{
    double[] times = took.OrderBy(x => x).ToArray();
    total += times.Length;
    double P(double q) => times.Length == 0 ? 0 : times[Math.Min(times.Length - 1, (int)(times.Length * q))];
    Console.WriteLine($"{name,-14} {times.Length,7} {times.Length / seconds,7:0.0} {P(0.5),8:0.0} {P(0.95),8:0.0} {P(0.99),8:0.0} {times[^1],8:0.0}");
}
Console.WriteLine($"{"all",-14} {total,7} {total / seconds,7:0.0}");
foreach ((string name, int n) in errors.OrderByDescending(kv => kv.Value)) Console.WriteLine($"error {name}: {n}");

if (!keep)
{
    await Parallel.ForEachAsync(sessions.Concat(idle).Where(s => s.Length > 0), new ParallelOptions { MaxDegreeOfParallelism = 16 },
        async (s, _) => await Call("cleanup", HttpMethod.Delete, "/v1/account", s));
    Console.WriteLine("Heroes deleted.");
}

string Arg(string name, string fallback)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}
