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
| Playtest | Phase 0 grey-box playtest done, owner reported it fine. First on-device playtest against the live server on the owner's iPhone, 23 Sep 2026: owner reported it good. |
| Art direction | B, modernized classic (GDD section 14), picked by the owner on 23 Sep 2026. D (ink and ember) was the runner-up. |
| Upgrade glow | Every equipped item glows from +7 upward, not only the weapon (owner, 23 Sep 2026). Weapon brightest; intensity steps at +8 and +9. Drives the glow shader in the URP switch. |
| Reference sheets | Chosen by the owner on 23 Sep 2026, all in `docs/concept/`: `vanguard-1`, `korstone-1`, `kestrel-1`, `wraithsworn-2`, `drumcaller-2`, `wolf-2`, `glow-1` (`-sheet.jpg`). The other variant of each is kept for comparison only. Every sheet after the Vanguard was generated with `vanguard-1` as the style reference; keep doing that for new sheets. |

## What exists and works

- `src/Orsuun.Rules`: engine-free rules (Forge, etchings/Turnstone, offline settlement, lane combat, zones, bosses,
  gear, XP/levels). 71 xUnit tests in `tests/`. Numbers pinned to the GDD (52.6 scrolls to +9, Turnstone odds, and so on).
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
  the alternative. Owner picked B on 23 Sep 2026.

## Next steps, waiting on the owner

1. Playtest server is live since 23 Sep 2026: https://65.108.221.210.sslip.io (Hetzner CPX12, Helsinki, Ubuntu 26.04,
   2 GB RAM + 2 GB swap, `/opt/orsuun`, Docker stack from `deploy/`, `ASPNETCORE_ENVIRONMENT=Development` for the
   playtest). Update with `ssh root@65.108.221.210 'cd /opt/orsuun && git pull && docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build'`
   (the box has a read-only deploy key on the repo). Mobile builds: on the Mac (Unity 6000.0.32f1 + Android/iOS
   modules installed) `ORSUUN_SERVER_URL=https://65.108.221.210.sslip.io tools/build-mobile.sh both`. TestFlight needs
   the paid Apple Developer Program; a free Apple ID runs on the owner's own iPhone from Xcode. First builds done 23 Sep 2026:
   Android APK served to testers at https://65.108.221.210.sslip.io/downloads/Orsuun.apk (Caddy file_server over
   `/opt/orsuun/downloads`, copy a new APK there after each build); iOS Xcode project at `client/Builds/iOS/Unity-iPhone.xcodeproj`. Installed on the owner's iPhone 15 Pro Max on
   23 Sep 2026 with the free personal team (`DEVELOPMENT_TEAM=KCT3PJSUP2`, cert on the Mac keychain). Signing lasts 7 days;
   to reinstall, plug the phone in and run from `client/Builds/iOS`:
   `xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -sdk iphoneos -destination 'id=00008130-00026DA40CC1001C' -derivedDataPath ../iOS-derived build DEVELOPMENT_TEAM=KCT3PJSUP2 CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates -allowProvisioningDeviceRegistration -quiet`
   then `xcrun devicectl device install app --device 00008130-00026DA40CC1001C ../iOS-derived/Build/Products/Release-iphoneos/OrsuunWarofBanners.app`.
   Developer Mode must be on (Settings, Privacy & Security). `tools/build-mobile.sh ios` with `ORSUUN_APPLE_TEAM_ID=KCT3PJSUP2`
   also works but a free team cannot export an .ipa, so the xcodebuild + devicectl route above is the one to use.
2. Art direction is B; turnaround sheets for all four classes, the Korstone and the Hollowed wolf are done and chosen
   (`docs/concept/`). The upgrade-glow progression sheet is done too. The full remaining art backlog (second-sex class variants, Forgemaster, more
   Hollowed mobs, Commander sheets, three environment keys, Oathfields backdrops, town vistas, gear and consumable and
   Korshard icons, UI colour pass, Banner emblems, Forge VFX boards, app icon) is the checklist in GDD section 14.1.
   Engineering next: URP switch with the glow shader, first real hero model from `vanguard-1`.
3. Later: second class (Wraithsworn Voidpact), Bannerkin companion, sixth etching, Temper, Oath Renewal (GDD section 12),
   real shared boss HP pools, Hunt Marks and the Hearthfire subscription (Bulk Turn's 10/50 split depends on it).

## Known gaps

- `ORSUUN_RESET_DB=1` wipes the schema on a Development start; keep it out of any shared environment.
- The live lane's loot is display only; each heartbeat replaces it with the server's settlement.
- Manual skill timing does not earn the active-play bonus server-side yet (needs an input log the server replays).
- Boss damage ranks are against simulated rivals. One account per device is not enforced yet.
- Dev credentials (`orsuun` / `orsuun-dev`) are for local PostgreSQL only. The Hetzner box has its own random password in
  `/opt/orsuun/deploy/.env` (git-ignored).
- `appsettings.json` pins `Urls` to localhost:5080; in the container only the unprefixed `URLS` env var overrides it
  (`ASPNETCORE_URLS` loses to the JSON file). `deploy/docker-compose.yml` sets it.

## Blender pipeline (Mac, since 23 Sep 2026)

Blender 5.2 LTS is installed with the MCP for Blender addon (user config `~/.claude.json`, server `uvx mcp-for-blender`).
Start Blender, press N, BlenderMCP tab, Start MCP Server; Claude can then build and edit the open scene. First asset:
`art/blender/korstone-blockout.blend` (procedural Korstone with an emissive ember-crack material) exported to
`client/Assets/Orsuun/Models/Korstone_blockout.fbx`. Hyper3D Rodin and Hunyuan3D image-to-3D are available in the
addon panel but not enabled; the reference sheets in `docs/concept/` are the inputs for them.

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
