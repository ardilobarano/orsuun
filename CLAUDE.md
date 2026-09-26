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
  Forge attempt asks first; any owned piece, worn or in the bag, can be forged and turned. (25 Sep) The premium
  currency is Amber, real money only; skins, mounts and companions are held 1/3/5/7/14 days with small stats (skin HP,
  mount attack, companion hunting XP or sorn), sold at the Caravan, and bosses from Gorak Pass on drop short ones. An
  account (Login) has up to 4 named characters and a shared 40-piece depot; Amber and the Banner are the account's.
- Designs must be original: nothing that reads as another game's character (a first Tul-Gorak came out as Kratos and
  was redone). Characters may be muscular or curvy but stay clothed and non-explicit (store ratings); the women wear
  the shortest shorts with garters (owner, 24 Sep).
- Art: generate sheets with the chosen `docs/concept/vanguard-1-sheet.jpg` as the style reference. Since 26 Sep 2026
  everything goes through Higgsfield (the owner's Ultra plan; the owner does not want to top up fal): sheets with
  gpt_image_2_5, 3D with Tripo H3.1 multiview (`tripo_h3_1_multiview_to_3d`, 9 credits; faces +X, so
  `yaw_degrees=-90`), upscales done locally; Blender then cuts, scales, rigs and exports. fal.ai (key in
  `~/.config/fal/key`, never in chat or git; `tools/art/fal.py`) is out of balance and optional. Item looks go through `art/blender/looks.py` (armour looks are rigged there by
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
- The painted kit pieces (`docs/concept/ui-kit`) are cut by `tools/ui/cut_ai_kit.py`, which writes their borders to
  `Resources/UI/Borders.json`; rerunning `make_ui_kit.py` alone overwrites them with the procedural ones, so run both.
- Screen scenes live in `Resources/Scenes` (non-power-of-two on purpose; `SceneArtImport` turns off NPOT scaling for
  that folder). A screen names its scene in `Ui.Backdrop(canvas, "Name")`.
- `ChatPanel` stays off its canvas (it polls world chat for the lane ticker while hidden), like the `Tutorial`.
- `WorldClock` (a hosted service, every 30 s) pairs war nights, settles guild wars and moves fortress keeps through
  their week, each under its own row locks; endpoints refuse out-of-window actions themselves. Guild treasuries are
  credited from outside the guild screen (keep refunds, the Exchange tax share) with single atomic UPDATEs, like guild XP.
- A new `.cs` in `client/Assets` (or `src/Orsuun.Rules`) has no `.meta` until Unity imports it: the first
  `ProjectSetup.BuildMac` after adding one fails to compile it; run it again (and commit the `.meta`).
- This Mac's locale writes decimals with a comma: parse and format numbers with `CultureInfo.InvariantCulture`.
- Enemies are rigged (`art/blender/mobrig.py`); a new mob goes through `looks.mob_model(..., rig=plan)` or it will have
  no clips (LaneView then falls back to the old procedural bob and keel-over).
- Friends (`Friendships`, one row per pair: asked by From, `Accepted` once taken) and guild invites (`GuildInvites`) are
  each hero's; code that deletes a hero or disbands a guild removes them, and blocking removes a friendship. Requests
  that name another hero send an id or, with `ServerLink.NoId`, a name (`FindHeroAsync`).
- The way in (owner, 25 Sep 2026): title, sign-in screen (new install or signed out: `AccountPanel.FirstScreen`), the
  account's Banner oath (`/v1/lobby/banner`), then the character screen, all driven from `GameRoot.LateUpdate`. The
  character screen covers the lane while `ServerLink.WaitingForHero`: the game must never show before a hero is chosen.
- Characters: `Account` is a character; `Login` is the player's account (email, password, Amber, Banner, devices,
  Google / Apple links). Show names with `NameOf(account)` / `ShownName(id, name)`, never `Banners.GeneratedName(id)`
  (older rows only). Items in the depot (`DepotLoginId`) are out of the bag: filter bag pieces with `Item.OutOfBag`.
  `/v1/auth/guest` without `lobby` keeps making a first character for older clients; online screenshots need `-autoselect`.
- Wardrobe stats and Oath Renewals: HP and attack are in `HeroFactory.FromEquipment(..., worn, renewals)` on both sides (server `Hero(account)`,
  client `PlayerSession.SetWorn` / `SetRenewals` from the state), so loop replays match; a companion's XP/sorn is added only by the
  server's `Apply(..., hunt: true)`. Duels/Pits (`Duels.Neutral`) ignore the wardrobe. Amber packs are free only on a
  Development server (`/v1/caravan/amber` answers "store_closed" elsewhere) until store purchases are built.
- The Campaign Trail (`Rules/Trail.cs`) is each character's; seasons run 8 weeks from Mon 21 Sep 2026 20:00 server
  time. A new season needs its costume (a model per class) and mount in `CampaignTrail.Themes` before it opens, or it
  repeats the last one. Anything that spends or grants Amber locks the login row first (`LockLoginAsync`): four
  characters share it.
- A piece on a direct trade's table has `Item.TradeId` and is out of the bag (`OutOfBag`), like a listed or depot piece;
  code that ends a trade must release it (`ReleasePiecesAsync`). Direct trade's level and age rules are off on a
  Development server.
- A dungeon's pause floor (`DungeonDef.SmithFloor`) holds the Chained Smith or the Carvers' rune lock (`DungeonDef.Pause`);
  the waiting run is `Account.DungeonRunAtSmith` and its dungeon `Account.DungeonPausedId`. A rune lock's riddle comes
  from the run id (`Dungeons.RiddleFor`), so it needs no storage.
- Pit seasons are settled by `WorldClock` (`SettlePitSeasonAsync`, claimed by the `PitSeasons` row). Never call the
  Development `/v1/dev/pit-season-end` on the playtest server: it settles the running season and halves every rating.
- Campaign stages run 1..100 (ten maps) and zone ids start at 101 (`Content.FirstZoneId`): maps 11 and 12 must move the
  zones (and migrate `Accounts.ParkedStage`) before they are added. Dungeon floors are 301-399.
- Higgsfield takes local images through `media_import_url` (stage them briefly in a random folder under the playtest
  server's `/opt/orsuun/downloads`, delete it afterwards) or `media_upload` (presigned PUT).
- Lane floors are `Resources/Floors/<backdrop key>` (owner picked each, 25 Sep 2026): a new backdrop needs its own floor
  (tileable, a road across the tile's middle if any) or the lane falls back to the plain stripes.
- Skill grades and Technique Scrolls (`Rules/SkillGrades.cs`) are kept for all twenty skills by book id (class * 5 +
  slot, since migration `FiveSkills`); the hero fights with its class's five (`SkillGrades.ForClass`) on both sides, so
  a grade that rises settles and reseeds the lane like a class change. Scroll stacks are `BookStacks` rows (`AddBooks` on the tracked hero,
  `AddBooksElsewhereAsync` upsert for anyone else). `MarketListing.BookId` is -1 for a piece: client DTOs initialise
  `bookId = -1` (JsonUtility leaves a missing int at 0), and a scroll listing sends the empty Guid as `itemId`.
- The Gear screen is the INVENTORY (mockup D): `GearPanel` keeps its name; its `HeroStage` stands at `Below + (60,0,0)`
  and switches off with the canvas (the panel lives on it). Material tiles come from `GearPanel.Goods`: a new material
  or token gets a row there with its icon.
- Skills 4 and 5 unlock by `HeroStats.Level` (`SkillDef.UnlockLevel`); a mounted hero (`HeroStats.Mounted`, from the
  worn wardrobe) casts nothing. Both come from `HeroFactory.FromEquipment` on both sides, so replays match; anything that
  changes the worn wardrobe settles and reseeds the lane (`WearAsync`).
- Cast animations are not in the FBX files: `CastClips.Ensure` builds them on a model's Animation and must run right
  after the model is instantiated (the bones' pose then is the rest pose). A Blender pose rotation (x, y, z) lands on the
  imported bone as (x, -y, -z), a hips offset as (-x, y, z). Skill effects (`SkillFx`) are additive sprites tinted from
  `Resources/Fx` (white on alpha) at `SkillFx.Brightness`: layers stack under the bloom, so keep new ones dim.
- The lane camera is at (1.5, 5.4, -19.5) looking at (1.5, 1.9, 0) in `GameRoot.BuildCameras` and three editor
  previews in `RenderingSetup`, with the backdrop at y -1.2: change them together.
- Private messages (`PrivateMessages`, owner 26 Sep 2026: kept "after days and days") have no expiry job: only a
  conversation past `Whispers.KeepPerConversation` loses its oldest lines. Deleting a hero deletes its messages both
  ways. A reported one is copied into `ChatMessages` (channel "w:" + the recipient's id, hidden) so the moderation page
  handles it; no chat reads "w:" channels, keep it that way. Sending shares chat's flood limit (`LastChatUtc`) and mutes.
