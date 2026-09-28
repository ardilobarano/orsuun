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
- Tripo multiview takes its views in the order front, LEFT, back, right. The left view is the one facing the viewer's
  left: most class sheets' side panels face right (pass front, the side mirrored, back, the side), but some face left
  (Kestrel T2; every Caravan animal sheet) and go in as drawn, mirrored for right. A wrong one comes out two-faced or
  backwards in profile. The ~30 models built the wrong way were made again on 27 Sep 2026; `looks.py` keeps its repairs
  (`FACE_FORWARD`, `BACKWARDS`, `BACK_FACES`, all empty now, and `turn_reversed_feet`) for one that comes out wrong.
  Check a new model in profile and from behind (the lane shows heroes side on) before adding it.
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
  repeats the last one (the Caravan shows the running season's two pieces only: its grid holds eight a tab). Anything that spends or grants Amber locks the login row first (`LockLoginAsync`): four
  characters share it.
- A piece on a direct trade's table has `Item.TradeId` and is out of the bag (`OutOfBag`), like a listed or depot piece;
  code that ends a trade must release it (`ReleasePiecesAsync`). Direct trade's level and age rules are off on a
  Development server.
- A dungeon's pause floor (`DungeonDef.SmithFloor`) holds the Chained Smith or the Carvers' rune lock (`DungeonDef.Pause`);
  the waiting run is `Account.DungeonRunAtSmith` and its dungeon `Account.DungeonPausedId`. A rune lock's riddle comes
  from the run id (`Dungeons.RiddleFor`), so it needs no storage.
- Pit seasons are settled by `WorldClock` (`SettlePitSeasonAsync`, claimed by the `PitSeasons` row). Never call the
  Development `/v1/dev/pit-season-end` on the playtest server: it settles the running season and halves every rating.
- Campaign stages run 1..120 (twelve maps, all of the world bible's) and zone ids start at 201 (`Content.FirstZoneId`;
  they were 101-121 until migration `MoveZones` moved `Accounts.ParkedStage`, 26 Sep 2026). Name zones by their
  constants (`Content.EmberSteppe` .. `Content.GorakWarCamp`), never by number. Dungeon floors are 301-399.
- Higgsfield takes local images through `media_import_url` (stage them briefly in a random folder under the playtest
  server's `/opt/orsuun/downloads`, delete it afterwards) or `media_upload` (presigned PUT).
- Lane floors are `Content/Floors/<backdrop key>` (owner picked each, 25 Sep 2026): a new backdrop needs its own floor
  (tileable, a road across the tile's middle if any) or the lane falls back to the plain stripes. Its scenery set
  (`LaneScenery.SetFor`, else Steppe) comes from `Content/Scenery` (atlases and `Rects.json` by `tools/art/scenery_atlas.py`).
- Skill grades and Technique Scrolls (`Rules/SkillGrades.cs`) are kept for all twenty skills by book id (class * 5 +
  slot, since migration `FiveSkills`); the hero fights with its class's five (`SkillGrades.ForClass`) on both sides, so
  a grade that rises settles and reseeds the lane like a class change. Scroll stacks are `BookStacks` rows (`AddBooks` on the tracked hero,
  `AddBooksElsewhereAsync` upsert for anyone else). `MarketListing.BookId` is -1 for a piece: client DTOs initialise
  `bookId = -1` (JsonUtility leaves a missing int at 0), and a scroll listing sends the empty Guid as `itemId`.
- The Gear screen is the INVENTORY (mockup D): `GearPanel` keeps its name; its `HeroStage` stands at `Below + (60,0,0)`
  and switches off with the canvas (the panel lives on it). Material tiles come from `GearPanel.Goods`: a new material
  or token gets a row there with its icon.
- `rig.rig_humanoid` pins what distance weights tore: the Drumcaller's drum to forearm.L (`pin_drum`), staff charms to
  hand.R (`pin_staff_charms`), the Wraithsworn's floating flame into hand.L (`hold_loose`), and eases hip cloth off the
  drum arm and the void hand (`free_cloth_from_arms`). Local screenshots: `-wear <look,look>` wears wardrobe pieces,
  `-class X -figure Man|Woman -stage 70 -plus 3 -castshow <slot>` shoots a skill (skill 5 needs level 60).
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
- Item icons are per level band (`Resources/Icons/Items/<Slot>_T<band>`, weapon and armour per class as
  `<Class><Slot>_T<band>` except the Vanguard's): use `Ui.ItemIcon(item)` (it follows `Ui.IconClass`, which GameRoot keeps
  on the playing hero's class, and falls back to the slot icon), never `item.Slot.ToString()`.
- Weapons of item level 30+ carry `AverageDamagePercent` / `SkillDamagePercent` (`WeaponRolls`, rolled in
  `HuntYield.DropGear`: two extra draws on the lane's RNG for such a weapon, so seeded balance tests use 60 seeds).
  They travel on `Item.AverageDamage` / `SkillDamage` and `ItemDto`; anything that copies an ItemState must copy them.
- The upgrade glow is the shader (sheen, rim, glitter; `_GlitterScale` per material, glaives finer) plus `GearSparkle`
  particles on each glowing piece (made by `GearSparkle.On`, on the piece's layer). Shared look materials (HeroStage)
  take the glow through property blocks; the lane and ItemPreview use instanced materials.
- The Vanguard since 26 Sep 2026 (`docs/concept/looks/vanguard2-T*.jpg`, sources `art/blender/vanguard2_T*-tripo.glb`,
  `look-weapon2-t*-tripo.glb`): armour through `looks.armor_look(..., yaw_degrees=-90, plain_pole=True)` (he holds a bare
  pole that is cut away; a `WeaponGrip` empty marks his fist), weapons through `looks.weapon_look`. The weapon kind of a
  band (`ItemLooks.WeaponKinds`) decides the fit in `LaneView.LayWeapon`: a glaive stretches along the pole, a sword
  rises from the fist at a share of the pole's length (`SwordSpan`). Weapon models import readable (particles need it).
- Downloaded art (owner, 26 Sep 2026: download on first launch like other games): the 3D models, their materials, the
  lane backdrops and floors live in `client/Assets/Orsuun/Content/` (not Resources), are packed per platform into asset
  bundles by `ContentBundles` during every player build, and phones fetch them from the server's
  `/downloads/content/<platform>/` on first launch (`ArtLoader`, a progress bar on the title art; later only changed
  bundles). `tools/build-mobile.sh` uploads Android/iOS bundles (bundles first, manifest last); the Mac build carries
  its own in StreamingAssets. Load that art only with `Art.Load<T>("Models/Looks/Armor_T3")` (bundle, else Resources,
  in the editor the project), never `Resources.Load`. New models export to `Content/Models/...` (looks.py). An app
  build whose asset keys change needs its bundles uploaded before testers open it.
- The bag is `Rules.Bag.Size` (120) loose pieces (owner, 26 Sep 2026): the server's `Apply` never removes a stored piece;
  new drops that do not fit are left behind (`Bag.Fitting`, counted in `SettlementDto.LeftBehind`), and the client's
  `PlayerSession.LeaveBehindOverflow` drops only pieces without a server id. Pieces are sold to the merchant by hand
  (`/v1/bag/sell`, `Bag.SellPrice`), never automatically.
- Exchange goods (`Rules.TradeGoods`, stored as `MarketListing.GoodId`/`GoodCount`; -1 means none, 0 is the Draught):
  ids are on listings (and letters), so append goods, never renumber.
- The mailbox (`Rules.Mail`, `Letters`, since 27 Sep 2026): `SendLetter` is one insert, so any request may write to any
  hero without touching its row. The Exchange pays sellers and returns what ran out by letter (a returned piece keeps
  `Item.InMail`, out of the bag like a listed one: SQL bag counts must add `!i.InMail`). Taking locks the hero's letters
  (`FOR UPDATE`); a piece waits while the bag is full. New money or items owed to a hero who is not the request's own
  should go by letter too.
- Every character is a man or a woman (`Rules.Figure`, since 26 Sep 2026); `ItemLooks.SecondLook(class, figure)` says
  when the class's other figure is shown: second-look models are named with "Alt" (`LaneView.AltName`: `ArmorAlt_T3`,
  `KestrelAlt_T3`), and `ClassLookName` / `SetLooks` / `HeroStage` fall back to the first look when one is missing.
  Pass the flag wherever a hero is drawn (lane, HeroStage, rivals: `DuelResultDto.DefenderFigure`).
- The login calendar (`Rules.DailyLogin`) is the account's: `Login.DailyDay` / `DailyClaimedOn` change only under
  `LockLoginAsync` (four characters share it), and a claim is keyed by `Bounties.DayKey` (the 20:00 bounty day).
- Text is translated where it is shown (`Loc`: Turkish since 27 Sep 2026, then German, Polish, Portuguese, Romanian):
  `Ui.Label` makes a `LocText`, which looks each piece up in `Resources/Loc/<code>.txt` (tr, de, pl, pt, ro: English, a
  tab, the translation; `{0}` holes, `{0#}` a number). Players' words go on `Ui.Raw(label)`; code that reads a label back
  compares `Ui.Src(label)` (the English), never `.text`. A new string needs its line in every language's file (a missing
  one shows in English): `-lang tr -locmiss <file>` on the Mac player writes the pieces that found none. Capitals go
  through `Loc.ToUpper` (Turkish i to İ only in Turkish), never `ToUpperInvariant` on translated text.
- `Resources/server-url.txt` (git-ignored) is baked by phone builds and stays, so a later Mac player talks to the playtest
  server: online screenshots against the local server pass `-server http://localhost:5080` (and `-autoselect`).
- Weekend events (`Rules.WorldEvents`) are `WorldEvents` rows: `WorldClock` writes the weekly calendar a week ahead
  (ON CONFLICT on Kind + StartsUtc, so a called-off weekly row is not written again), says each in world chat once and
  reloads `EventCalendar`, the singleton requests read (never query the table per request). Moderators add or call off
  events on /admin's Events tab; `/v1/dev/event` starts one now (Development). Commander clocks move only through
  `RollClock` (spawns come faster in a rush): never step `BossDef.RespawnSeconds` by hand. Kinds are stored by number.
- Riders (`LaneView.FitRider`): a mount's saddle and barrel are measured from its mesh (`Seat`), so mount models
  (`Models/Mobs/Mount*`: a new mount's name must start with Mount) import readable; the legs are aimed in the rider's
  own frame, never by per-bone Euler angles (the rigs' left and right thighs have mirrored axes).
- Creature models (`looks.mob_model`: enemies, mounts, companions) weld the mesh's UV-seam splits before decimating
  (`_bake(weld=True)`); decimated split, the seams opened into hairline cracks the lane's bright ground showed through.
  Hero looks (`class_look`, `armor_look`) keep the split mesh: their weapon cut (`_held_islands`) relies on it.
  A source already decimated before the weld (split pieces, nothing to weld) goes through `art/blender/mob_repair.py`
  first (voxel remesh, keeping the source's own UV charts and texture). Every enemy's settings are in
  `art/blender/mobs/builds.json`; `build_mob.py -- <Name>` rebuilds one. A robed biped takes `robe` (its robe eased off
  the arms and legs, a gliding run) and the `cast` attack: a staff standing on the ground bends if its hand swings.
- Achievements (`Rules.Achievements`, since 27 Sep 2026) read lifetime counters (`Account.Feats`, `FeatMetric`: its
  first seven follow `BountyMetric`, so every `Count` also counts a feat; append new metrics, never renumber) and the hero
  as he stands; achievement ids are stored in `FeatsClaimed` and `TitleId`, so never renumber them either. A worn title
  goes on chat lines (`ChatMessage.Title`) and Pit boards (`TitleOf`: the worn title, else the Pits' season title).
- Guild raids (`Rules.GuildRaids`): a `GuildRaids` row per guild and bounty week, made by the week's first fight
  (INSERT ... ON CONFLICT) and changed only under FOR UPDATE; `GuildRaidHits` give the day's fights and the shares. When
  the boss falls, fighters are paid Tallies (the fighter's own row tracked, others by single UPDATEs) and sorn by
  letter. Code that removes a guild calls `DeleteRaidsAsync`. The client replays a fight with `BossRun.Create(stage, ...)`.
- Password reset and email checks (`GameService.Recovery`, since 27 Sep 2026) send 6-digit codes through `MailSender`
  (`Mail:Host/Port/User/Password/From`, from `MAIL_*` in `deploy/.env`); only a hash is stored. A Development server with
  no SMTP keeps each email for `/v1/dev/mail?email=` (the smoke test reads codes there); elsewhere "forgot" answers
  "mail_off". FORGOT answers the same whether or not the email has an account; a reset binds the device like a sign-in.
- Phone performance (`Performance`, on GameRoot): the URP asset's `renderScale` is set at runtime (the lane at most 1800 px
  tall; BATTERY SAVER draws it smaller and turns off bloom), 60 fps while touched and 30 when left alone. Never switch
  MSAA at runtime: on Metal it turns the frame upside down and blacks the lane.
- Hunting is paid by estimate (`HuntYield.Settle`) at every heartbeat (30 s online), with the account's `HuntCarry`
  (`HuntCarryTicks`, `HuntEncounter`): the part of an encounter and the place in the loop a settlement leaves for the
  next. Pass it on every settle and reset it when the parked stage changes; without it a slow hero is never paid a
  Korstone (a heartbeat holds under one loop).
- Screens that open by level (`Rules.Unlocks`, 27 Sep 2026) are gated on the client through `GameRoot.Unlocked(Feature)`
  (bottom bar, SHARDS, the Banner flag, dungeons and Commanders in ZONES, the Pits in WAR); a new way into one of them
  must ask it too. The server does not enforce them.
- A won push moves the hunt when the hero hunts the campaign's front (`Content.HuntFollowsPush`, server `PushAsync` and
  `PlayerSession.Push` alike). The goal line's reached step is saved per hero as `orsuun.goalSteps2.<name>`: reordering
  `Goals.Chain` needs a new key (the line then starts again and passes what is done).
- Funnel milestones (`GameService.Funnel`): `Mark(account, name)` queues a first, written after `SaveAsync` with ON
  CONFLICT (one row per hero and name). `Feat` marks "first-<metric>" by itself; a new first elsewhere calls `Mark` and
  gets a row in `FunnelSteps`. Phones may send only `tutorial-*` names.
- Music (27 Sep 2026) is downloaded art: `Content/Music/<Name>.mp3` (ElevenLabs Music), played through
  `GameAudio.Music(name, fallback)`; `GameRoot.UpdateMusic` picks it from the lane's backdrop (`MapMusic`). A new map or
  backdrop needs its entry there; a new theme that fades in or is louder than about -17.5 dBFS gets a row in
  `GameAudio.Tracks`. Higgsfield cannot make music (its audio tools are speech only).
- Store purchases: Amber is credited only by `/v1/caravan/purchase` after `StoreReceipts` has checked the receipt with
  Apple or Google, once per `(Store, TransactionId)` (`Purchases`), under `LockLoginAsync`. The phone confirms a purchase
  to the store only on a final answer (`ServerLink.AmberPurchase`), never on "store_closed" or no answer. The store keys
  live in `deploy/secrets` (git-ignored, like the fal key: never in chat or git). Store code compiles only with
  `UNITY_ANDROID || UNITY_IOS` (ClientCheck does not see it: check a phone build).
- Pushes (`PushSender`): every `SendLetter` queues one, sent after the request's save (`SendPushes`, from `SaveAsync`; code
  that saves another way calls `SendPushes` after its commit). The phone's token goes to `/v1/push-token`; iOS registers
  only with the `ORSUUN_PUSH` define (the push entitlement breaks free-team signing). Keys in `deploy/secrets`.
- Old Nergui's river (`Rules.Fishing`, 28 Sep 2026): a hero at the river (`Account.AtRiver`) hunts nothing (`Settle`
  counts no seconds, the client's lane stands still while `ServerLink.AtRiver`); going there settles first, leaving
  reseeds the lane like a park (`LeaveRiver`, also called by `ParkAsync`), and pushes are refused there. The Tireless
  Rod's catches are counted on each heartbeat (`LandAuto`). A fish eaten adds its XP/sorn to hunting in `Apply(hunt:)`
  for the share of the interval it lasted, like a companion (never in the lane's combat, so replays are untouched).
  `RiverPanel` lives off its canvas and shows itself while the server says the hero is at the river.
- Invites (`Rules.Invites`): an invited hero reaching level 10 is caught by `CheckInvite` in `Apply` and paid by letter
  in `SaveAsync`; a new place that writes XP directly must call `CheckInvite` too.
- SETTINGS (`GameSettings`, in Performance.cs): text size scales every `LocText` made by `Ui.Label` (its base size is
  set there; a size a screen sets later becomes the base) and the inline `<size=n>` tags in its text; FEWER skill
  effects skips `SkillFx.Layers`, the spirit and the runes. Graphics LOW replaced the BATTERY SAVER (`Performance.Saver`).
- The river is a 3D place (`RiverScene`, at the river stage `HeroStage.Below + (-120,0,0)`, drawn by that stage's camera
  with bloom and a 260 m far clip): while it shows, the lane and backdrop cameras are off and the Sun and ambient are the
  evening's (`Show`/`Hide` restore them). Its textures and materials are `Content/River` (`RenderingSetup.EnsureRiver`);
  the water is `Shaders/Water.shader`, reflecting the horizon quad by its `_SkyRect`/`_SkyZ`: move the quad, move those.
  Nergui's model faces its -x (`NerguiYaw`, `FireOffset`); `-rivershot nergui` looks at his camp.
- The Bannerkin (`Rules.Bannerkin`): her pieces are items with `Kin`; the ones she wears have `KinWorn` (out of the bag,
  never `Equipped`), so hero queries on `Equipped` stay the hero's. SQL bag counts add `!i.KinWorn`; a broken piece goes
  through `BreakPiece`. Her stats reach combat only as `HeroStats.Kin` from `FromEquipment(..., kin:)` on both sides.
