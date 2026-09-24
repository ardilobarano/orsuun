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
  looks change every 10 item levels (`ItemLooks`). Helmet, shield, jewellery, shoes are stats only. (24 Sep) every
  Forge attempt asks first; any owned piece, worn or in the bag, can be forged and turned.
- Designs must be original: nothing that reads as another game's character (a first Tul-Gorak came out as Kratos and
  was redone). Characters may be muscular or curvy but stay clothed and non-explicit (store ratings); the women wear
  the shortest shorts with garters (owner, 24 Sep).
- Art: generate sheets with the chosen `docs/concept/vanguard-1-sheet.jpg` as the style reference. 3D via Hyper3D Rodin on
  fal.ai (key in `~/.config/fal/key`, never in chat or git; the Blender MCP tool's fal path is broken, call the fal queue
  API directly). Tripo H3.1 multiview on Higgsfield is the alternative (used while fal was empty on 24 Sep; faces +X,
  so `yaw_degrees=-90`). Item looks go through `art/blender/looks.py` (armour looks are rigged there by
  `rig.py`); other classes' bands through `looks.class_look`; enemies through `looks.mob_model`.
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
- Panels hide by deactivating their canvas; a component that must keep updating while hidden (the `Tutorial`) lives
  off its canvas.
- Store-release blockers that need the owner's accounts are listed in HANDOFF.md ("Store release").
- The Banners are Ember (red), Sky (blue) and Gold (yellow) since 24 Sep 2026; the world bible's older Jade and Bone
  names are gone (their creeds moved to Gold and Sky).
- Shared world rows (Commander pools, fortresses) are changed only inside a transaction that locks the row
  (`FOR UPDATE`); status reads never write pools. War points go through the `AddPointsAsync` upsert.
- `tools/build-mobile.sh` starts the editor with `-buildTarget` for the platform: the notification package's iOS
  post-processor (links UserNotifications.framework) only compiles with `UNITY_IOS` defined at startup.
- In zsh, `GID` (and `UID`) are read-only integer specials: a script that stores a guild id in `$GID` fails with
  "bad math expression". Use another name, or run the script with bash.
- Guild rows change only under `LockGuildAsync` (FOR UPDATE, then a reload, since `GuildOf` may already track the row);
  other accounts' guild fields change through single `ExecuteUpdateAsync` statements, never loaded and saved.
- macOS `/bin/bash` is 3.2: it brace-expands `"{..,..}"` JSON inside `"$(...)"`. Shell scripts build request bodies
  with `jq -n` into variables (see `tools/smoke-social.sh`).
- Sessions live on `Device` rows (one per device token); `Account.SessionToken` is only read for sessions handed out
  before devices existed. Items on the Salt Exchange stay with their seller with `Item.Listed` set: anything that
  lists, wears, forges or turns an owned piece must skip listed ones.
- UI art comes from the kit (`tools/ui/make_ui_kit.py` writes `Resources/UI`; power-of-two sizes only, since Unity's
  default import rescales others and breaks nine-slice borders). Use `Ui.Button` / `Ui.Framed` / `Ui.Backdrop` /
  `Ui.Bar` rather than flat `Ui.Panel` plates; a new skill needs `Resources/Icons/Skills/<letters of its name>.png`.
- The moderation page (`src/Orsuun.Server/Admin/`) is embedded in the server assembly; anything players wrote is put
  on it with `textContent` only (never innerHTML). Moderators come from `Admin:Emails` (env `Admin__Emails`).
- The Google OAuth client file lives in `~/.config/orsuun/google-oauth.json` (like the fal key: never in chat or git);
  the server reads `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` from `deploy/.env`.
- Google / Apple sign-in tickets are bound to the device token that began the flow (`ExternalAuth.Redeem`): keep it
  that way, or a sign-in link sent by someone else could move a hero. The game object must stay named "ServerLink"
  (the iOS sign-in sheet answers through UnitySendMessage).
- `ChatPanel` stays off its canvas (it polls world chat for the lane ticker while hidden), like the `Tutorial`.
- Enemies are rigged (`art/blender/mobrig.py`); a new mob goes through `looks.mob_model(..., rig=plan)` or it will have
  no clips (LaneView then falls back to the old procedural bob and keel-over).
