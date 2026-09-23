# Orsuun: War of Banners

Mobile side-scrolling idle auto-battler with Metin2-style risky upgrades, original IP. Owner decides design; ask before
changing anything the owner decided. Current state, decisions and next steps: **HANDOFF.md** (read it first). Design:
the GDD Claude doc linked from HANDOFF.md (edit with the docs tools, never web-fetch it).

## Layout

- `src/Orsuun.Rules` engine-free rules (netstandard2.1), shared with Unity as a local package. `tests/` xUnit.
- `src/Orsuun.Server` ASP.NET Core 8 + PostgreSQL, EF migrations. Server decides every roll; client sends intents.
- `client/` Unity 6000.0.32f1, URP 17.0.3, everything built in code by `GameRoot`.
- `tools/` Orsuun.Sim (balance), ClientCheck (dotnet type-check of Unity scripts), smoke tests, build scripts.
- `deploy/` Docker + Caddy stack for the Hetzner playtest server. `art/blender/` Blender sources and `looks.py`.
- `docs/concept/` reference sheets, icons, environment keys, previews.

## Commands (Mac)

```bash
export PATH="$HOME/.dotnet:$PATH"                     # .NET 8 SDK lives in ~/.dotnet
dotnet test -c Release                                 # rules tests
dotnet build tools/ClientCheck -c Release              # Unity scripts type-check without the editor
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Orsuun.Server   # http://localhost:5080
bash tools/smoke.sh [url]                              # walks every endpoint
U=/Applications/Unity/Hub/Editor/6000.0.32f1/Unity.app/Contents/MacOS/Unity
$U -batchmode -quit -projectPath client -executeMethod Orsuun.Client.EditorTools.RenderingSetup.RenderPreview   # headless renders to artifacts/
ORSUUN_SERVER_URL=https://65.108.221.210.sslip.io bash tools/build-mobile.sh both
tools/screenshot-mac.sh artifacts/x.png [-forge]       # after ProjectSetup.BuildMac
```

iOS device install, server deploy/update and the EF migration command are in HANDOFF.md.

## Rules of the road

- Rules numbers are pinned to the GDD; tests guard them. Warnings are errors.
- A new `.cs` in `src/Orsuun.Rules` needs a Unity `.meta`; opening the project in Unity (or RenderPreview) creates it. Commit it.
- EF migrations: `dotnet ef migrations add <Name> --project src/Orsuun.Server --msbuildprojectextensionspath artifacts/obj/Orsuun.Server/`.
- Owner decisions (23 Sep 2026): art direction B; every item forges like the weapon; only weapon and body armour are
  visible and glow (each by its own level, classic MMO shine, +7 pale gold, +8 gold, +9 ember-gold); weapon and armour
  looks change every 10 item levels (`ItemLooks`). Helmet, shield, jewellery, shoes are stats only.
- Art: generate sheets with the chosen `docs/concept/vanguard-1-sheet.jpg` as the style reference. 3D via Hyper3D Rodin on
  fal.ai (key in `~/.config/fal/key`, never in chat or git); the Blender MCP tool's fal path is broken, call the fal queue
  API directly. Item looks go through `art/blender/looks.py` (armour looks are rigged there by `rig.py`); enemies through
  `looks.mob_model`.
- Active play: the farm lane online is one seeded loop per encounter cycle; anything that rebuilds `PlayerSession.Lane`
  must go through `NewFarmLane`/`StartLoop`, and anything that changes the hero through `RefreshHero`, or loop reports
  stop matching the server's replay (`SessionLoopTests` guards this).

## Gotchas

- `appsettings.json` pins `Urls` to localhost:5080; in Docker only the unprefixed `URLS` env var overrides it.
- URP SRP Batcher is deliberately off (it crossed EmberGlow material colours on Metal).
- The editor compiles shaders asynchronously; headless renders set `ShaderUtil.allowAsyncCompilation = false`.
- Blender suffixes duplicate names (`WeaponBase.001`); code that finds parts by name strips the suffix.
- Unity rewrites `client/ProjectSettings/ProjectSettings.asset` on builds (BuildMac flips `runInBackground`); revert
  that churn. The default icon slot (`m_BuildTargetIcons`) is set on purpose by `ProjectSetup.EnsureAppIcon`.
- New guest accounts are capped at 10 per network per day; loopback is exempt.
- Skinned meshes only deform in the player loop: headless editor renders must bake them (`RenderingSetup.CaptureSkinned`).
- `Ui.Icon` fits its parent; give each icon its own box rect, never the canvas.
- Cinzel's 1 reads as a Roman I: use it only for screen titles without digits (`carved: true`); buttons use Philosopher.
- The playtest server runs in Development mode (dev endpoints open): share the URL with trusted testers only.
