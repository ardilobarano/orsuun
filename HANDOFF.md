# Handoff: state of Orsuun on 22 Sep 2026

Read this first in a new session or on a new machine. The README covers commands; this file covers what has been
decided, what exists, and what comes next.

## The project in one paragraph

Orsuun: War of Banners is a mobile side-scrolling idle auto-battler with Metin2-style risky upgrades, under an
original IP. The design document (GDD) with the world bible lives in a Claude doc:
https://claude.ai/code/artifact/d4c88b45-f61a-4cc5-bd1c-2e954e27bdfe (same claude.ai account; edit it with the docs
tools, do not web-fetch it). Code is in this repo, private on GitHub: https://github.com/ardilobarano/orsuun.

## Decisions already made (do not reopen without the owner)

| Topic | Decision |
| --- | --- |
| Title and IP | Original brand "Orsuun: War of Banners". No Metin2 names, art or text anywhere in the shipped game. |
| Names | World Orsuun; Metin stones = Korstones; yang = sorn; classes Vanguard / Kestrel / Wraithsworn / Drumcaller; three Banners instead of empires. Full glossary in the GDD's "World and naming bible" tab. |
| Forge burn rule | Items can be destroyed (Oathbreak) only from the +4 attempt upward; +1..+3 failures drop one level. |
| Server authority | Every roll, reward and trade is decided by the server. The client sends intents and replays seeds. |
| Storage | PostgreSQL from day one (dev runs it locally). |
| Map roles | Hunting Grounds (sorn, levels), Korstone Fields (materials, Turnstones, Korshards), Commander Grounds (bosses, skins). Campaign stages are the unlock spine. GDD section 13. |
| Boss brackets | Simulated rivals until the multiplayer milestone; real shared HP pools later. |
| Playtest | Phase 0 grey-box playtest done, owner reported it fine. |

## What exists and works

- `src/Orsuun.Rules`: engine-free rules (Forge, etchings/Turnstone, offline settlement, lane combat, zones, bosses,
  gear, XP/levels). 52 xUnit tests in `tests/`. Numbers pinned to the GDD (52.6 scrolls to +9, Turnstone odds, and so on).
- `src/Orsuun.Server`: ASP.NET Core 8 + PostgreSQL 16. Guest login, heartbeat settlement (live and offline), Forge,
  Turn, equip, park, push, boss fights with damage brackets, append-only ledger, idempotent request ids.
  `tools/smoke.ps1` walks every endpoint.
- `client/`: Unity 6000.0.32f1 grey-box, built-in render pipeline, everything built in code by `GameRoot`. Lane view,
  HUD, Forge / Gear / Zones panels, push and boss replays from server seeds, LOCAL MODE fallback.
- `tools/Orsuun.Sim`: Monte Carlo balance report. `tools/ClientCheck`: compiles Unity scripts with dotnet.

## Done since the first handoff (same day)

- Korshards and sockets (rules, server endpoints, SHARDS panel). Evening Bells and Bulk Turn.
- EF migrations (`Migrations/Initial`), Docker + Caddy deployment stack in `deploy/`, configurable server URL,
  `ProjectSetup.BuildAndroid` (needs the Android module; the Windows PC does not have it, the Mac should).
- Four art direction boards in GDD section 14 and `docs/art-options/`; recommendation B (modernized classic), D as
  the alternative. Waiting for the owner's pick.

## Next steps, waiting on the owner

1. Pick the host (Hetzner VPS recommended) and follow `deploy/README.md`; then on the Mac
   `ORSUUN_SERVER_URL=https://<domain> tools/build-mobile.sh both` (decided: Android and iOS). Install the Android
   and iOS modules in Unity Hub first. TestFlight needs the paid Apple Developer Program; a free Apple ID runs on
   the owner's own iPhone from Xcode.
2. Pick an art direction (GDD section 14), then: character sheet and Korstone sheet in that style, URP switch with a
   matching shader, first real hero model.
3. Later: second class (Wraithsworn Voidpact), Bannerkin companion, sixth etching, Temper, Oath Renewal (GDD section 12),
   real shared boss HP pools, Hunt Marks and the Hearthfire subscription (Bulk Turn's 10/50 split depends on it).

## Known gaps

- `ORSUUN_RESET_DB=1` wipes the schema on a Development start; keep it out of any shared environment.
- The live lane's loot is display only; each heartbeat replaces it with the server's settlement.
- Manual skill timing does not earn the active-play bonus server-side yet (needs an input log the server replays).
- Boss damage ranks are against simulated rivals. One account per device is not enforced yet.
- Dev credentials (`orsuun` / `orsuun-dev`) are for local PostgreSQL only.

## Running on macOS

```bash
# .NET 8 SDK
brew install --cask dotnet-sdk@8      # or https://dot.net
# PostgreSQL 16 (either)
brew install postgresql@16 && brew services start postgresql@16
#   or: docker compose -f deploy/docker-compose.dev.yml up -d
createuser -s orsuun 2>/dev/null; psql -d postgres -c "ALTER ROLE orsuun PASSWORD 'orsuun-dev';" -c "CREATE DATABASE orsuun OWNER orsuun;"
# Rules tests, simulator, server
dotnet test -c Release
dotnet run --project tools/Orsuun.Sim -c Release
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Orsuun.Server     # http://localhost:5080
# Smoke test (bash port): tools/smoke.sh
```

Unity: install 6000.0.32f1 through Unity Hub with the Android and iOS modules, open `client/`, play
`Assets/Orsuun/Scenes/Main.unity`. The Game view should be a portrait resolution such as 1080x1920. Headless:
`/Applications/Unity/Hub/Editor/6000.0.32f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath client ...` with the
same arguments as the README's Windows commands.

The Windows machine keeps its own PostgreSQL data (dev accounts only); nothing in it needs to move.
