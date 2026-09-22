# Orsuun: War of Banners

Side-scrolling idle auto-battler with a risky upgrade economy. Design lives in the GDD; this repo holds the code.

## Layout

| Path | What it is |
| --- | --- |
| `src/Orsuun.Rules` | Engine-free game rules: Forge, etchings and Turnstones, offline rewards. `netstandard2.1`, C# 9, no Unity or server dependencies. Also a Unity local package (`package.json` + `.asmdef`). |
| `tests/Orsuun.Rules.Tests` | xUnit tests. They pin the numbers the GDD publishes to players. |
| `tools/Orsuun.Sim` | Monte Carlo balance simulator. Run it after every rate change. |
| `src/Orsuun.Server` | ASP.NET Core 8 game API on PostgreSQL 16. Owns accounts, inventory, the weapon and an append-only ledger; runs the same rules library with a cryptographic RNG. |
| `client/` | Unity 6 (6000.0.32f1) grey-box: one lane, a Vanguard, mob packs, a Korstone with waves, and the Forge screen. Everything is built in code by `GameRoot`; the scene is empty on purpose. |
| `tools/screenshot.ps1` | Launches the Windows build and saves a PNG of its window. |

## Rules of the rules library

- **Server authority.** The server runs this code to decide outcomes. The client runs the same code only to display and predict.
- **No floats in gameplay.** Chances are basis points (10000 = 100%), sorn is `long`. `ForgeAnalysis` uses doubles and is for balance work only.
- **No hidden randomness.** Every roll takes an `IRandom`. The server passes a cryptographic generator, tests and replays pass `XorShiftRandom` with a seed.
- **Rules decide, callers pay.** `ForgeService.Attempt` returns the outcome; charging sorn, materials and the scroll is an inventory transaction in the caller.

## Commands

The .NET 8 SDK is installed per user in `%LOCALAPPDATA%\Microsoft\dotnet`. If `dotnet` on PATH reports no SDK, call that one directly or put it first on PATH.

```powershell
$dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
& $dotnet test -c Release
& $dotnet run --project tools/Orsuun.Sim -c Release            # 200,000 runs, fixed seed
& $dotnet run --project tools/Orsuun.Sim -c Release 50000 42   # runs, seed
```

## Server

PostgreSQL 16 runs as a user process (no Windows service, no admin): `tools\pg.ps1 start|stop|status|psql`. Data lives in `%LOCALAPPDATA%\Orsuun\pgdata`; the dev database is `orsuun` / user `orsuun` / password `orsuun-dev` (local only, see `appsettings.Development.json`).

```powershell
tools\pg.ps1 start
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& $dotnet run --project src/Orsuun.Server          # http://localhost:5080
```

| Endpoint | What it does |
| --- | --- |
| `POST /v1/auth/guest {deviceToken}` | Creates or finds the account, returns a session token for the `X-Session` header |
| `GET /v1/me` | Full state: inventory, weapon, Forge preview |
| `POST /v1/heartbeat` | Settles hunting time since the last heartbeat: live rate up to 3 min, offline rate (60%) up to 12 h beyond that |
| `POST /v1/forge {requestId, method}` | One Forge attempt. `requestId` makes retries safe; a repeat returns 409 |
| `POST /v1/turn {requestId}` | Turnstone reroll |
| `POST /v1/equip {requestId, itemId}` | Equips an owned piece; the old one returns to the loot list (capped at 60 loose pieces, best kept) |
| `POST /v1/park {stage}` | Moves the farm lane to a cleared stage; settles time on the old stage first |
| `POST /v1/push {requestId}` | Scores the next stage with a fresh seed and returns it; the client replays the seed so it shows the fight that was scored |
| `POST /v1/boss/fight {requestId, bossId}` | One Commander fight per spawn: scored with a seed, ranked against simulated rivals, chest by damage bracket. `/me` and `/heartbeat` carry the boss clocks |
| `POST /v1/dev/grant`, `/v1/dev/bosses-up` | Playtest grant and instant boss spawns, Development environment only |

Zones (GDD section 13) are park ids 100+: Hunting Grounds 101-103, Korstone Fields 111-115, Gorak War Camp 121. Campaign stages are 1-10. Fields IV-V and Commander Grounds never settle offline.

Content (maps, stages, item names, drop weights) lives in `Orsuun.Rules/Content.cs`, compiled into both sides. `ORSUUN_RESET_DB=1` on a Development start drops and recreates the schema; use it after model changes until migrations exist.

`dotnet build tools/ClientCheck` compiles the Unity scripts against the editor's assemblies, a type check that works while the editor holds the project lock.

Every roll and currency change lands in the `Ledger` table with the chance rolled against and the outcome. Concurrency is optimistic via PostgreSQL's `xmin`; a clash returns 409 and the client refreshes.

The client connects to `http://localhost:5080` by default (`-server http://host:port` to override, `-local` to skip). When the server is unreachable the HUD shows LOCAL MODE and rolls locally.

Known gaps before alpha: `EnsureCreated` instead of EF migrations; the live lane's loot is display-only and gets replaced by the server's settlement on each heartbeat; manual skill timing does not yet earn the active-play bonus server-side (needs an input log the server can replay).

## Playing the grey-box

- Double-click `client\Builds\Windows\Orsuun.exe` (not in git; rebuild with the command below), or open `client/` in Unity Hub and press Play in `Assets/Orsuun/Scenes/Main.unity`.
- Skills are the three big buttons, each with its own AUTO toggle. FORGE opens the Forge while the hunt continues behind it; GEAR shows the 8 slots and the best loose drops with EQUIP buttons. PUSH scores the next stage on the server and replays the fight; `<` `>` park the farm lane on any cleared stage. SPEED cycles x1, x3, x8. DEV grants sorn and consumables so a tester can reach +7 to +9 in one sitting.
- `Orsuun.exe -forge` starts with the Forge open.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Unity.exe"
# First-time setup (scene, player settings, GreyBox material):
& $unity -batchmode -quit -projectPath client -executeMethod Orsuun.Client.EditorTools.ProjectSetup.Run -logFile artifacts\unity-setup.log
# Play-mode smoke tests:
& $unity -batchmode -projectPath client -runTests -testPlatform PlayMode -testResults artifacts\playmode-results.xml -logFile artifacts\unity-playmode.log
# Windows playtest build:
& $unity -batchmode -quit -projectPath client -executeMethod Orsuun.Client.EditorTools.ProjectSetup.BuildWindows -logFile artifacts\unity-build.log
```

Grey-box shortcuts that are not the final design: built-in render pipeline instead of URP, a time-seeded local RNG instead of server rolls, a weapon that arrives with 5 etchings, and one hard-coded stage.

## Using the rules in Unity

In the client project's `Packages/manifest.json`:

```json
"com.orsuun.rules": "file:../../src/Orsuun.Rules"
```

Build output goes to `/artifacts` (see `Directory.Build.props`), so the package folder contains only source.
