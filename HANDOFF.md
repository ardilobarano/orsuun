# Handoff: state of Orsuun on 24 Sep 2026

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
| Every item like the weapon | Owner, 23 Sep 2026: any equipped item can be forged and turned with the weapon's costs, chances and methods; an Oathbreak replaces it with a starter piece for its slot. The Forge screen picks the item with a slot row. |
| Forge from the bag, and asking first | Owner, 24 Sep 2026: every Forge attempt asks for confirmation first (chance, full cost, what a failure costs), and pieces can be forged and turned from the Gear screen without equipping them. Assumption (not stated by the owner): an Oathbreak on a bag piece just destroys it; only a worn piece is replaced by a starter, so breaking junk cannot mint starters. |
| Kestrel, character styling | Owner, 24 Sep 2026: add a second class, commanders, title screen and hunt sound; make characters "a bit muscled up or with big tits and ass". Built as: heroic muscular men (Vanguard drawn 10% broader, Tul-Gorak), curvy women (Kestrel, the Mirage Queen), always fully clothed and non-explicit so store ratings stay in the teen band. Kestrel (Talon, paired knives) is the second class: Heartseeker 600% / 6 s with a 900% weak point, Knife Fan 130% / 10 s, Kestrel's Dive haste; 90% attack, 85% HP, +7% crit, 10-tick swings. Measured: pushes 7% faster than the Vanguard, aimed play 125%. Class switch is free and instant for the playtest (Gear screen). The Wraithsworn stays next on the roadmap. |
| All four classes, mob attacks, Korstone tiers | Owner, 24 Sep 2026. Wraithsworn (Voidpact: Void Lance 750%/8 s, Grave Tide, Pact Frenzy; 105% attack, 60% defense, 80% HP, 13-tick swings, weak point 700) and Drumcaller (Thunder Rite: Sky Hammer 450%/7 s, Storm Drum 200%/8 s, War Rhythm 7 s; 95/110/105%, +5% crit) join Vanguard and Kestrel; all four push within 10% of each other and pay 123-135% for aimed play (`ClassBalanceTests`). Korstones change with level: five tiers of 20 levels (Ember, Blood, Void, Grave, Khan colours, darker stone each tier) and three shapes (runed monolith, chained twin spire, crowned obelisk; an Elder takes the next shape up). |
| Outfits, Wraithsworn sword, class bands | Owner, 24 Sep 2026: the women wear the shortest shorts with garters (Kestrel and the Drumcaller: shorts, garter straps, thigh-high stockings), still clothed and non-explicit. The Drumcaller's first band keeps a mid-thigh tunic over them because the image generator refused the bare version; the filter was not worked around. The Wraithsworn attacks with the Kestrel's slash rhythm but with a sword (`rig.ATTACKS["sword"]`: wind-up overhead, slash with a chest twist). Kestrel, Wraithsworn and Drumcaller now have three looks each (T0-T2, every 10 item levels like the Vanguard). Assumption (not stated by the owner): their weapon look follows the armour's band (the blade or staff is part of the class model), while its glow still follows the weapon's own level. |
| Server authority | Every roll, reward and trade is decided by the server. The client sends intents and replays seeds. |
| Storage | PostgreSQL from day one (dev runs it locally). |
| Map roles | Hunting Grounds (sorn, levels), Korstone Fields (materials, Turnstones, Korshards), Commander Grounds (bosses, skins). Campaign stages are the unlock spine. GDD section 13. |
| Boss brackets | Simulated rivals until the multiplayer milestone; real shared HP pools later. |
| Playtest | Phase 0 grey-box playtest done, owner reported it fine. First on-device playtest against the live server on the owner's iPhone, 23 Sep 2026: owner reported it good. |
| Art direction | B, modernized classic (GDD section 14), picked by the owner on 23 Sep 2026. D (ink and ember) was the runner-up. |
| Upgrade glow and looks | Owner, 23 Sep 2026: only the weapon and the body armour are visible on the character and glow, each by its own level from +7 up, in classic MMO upgrade shine (aura, flowing light, sweep; pale gold +7, gold +8, ember-gold +9). Helmet, shield, bracelet, necklace, earrings and shoes are stats only: forged like the weapon, no look, no glow. |
| Item looks | Owner, 23 Sep 2026: weapon and body armour looks change every 10 item levels (`ItemLooks`: 11 bands, Vanguard names from Herder's Glaive / Quilted Coat up to Glaive / Harness of the Nine Oaths). Art exists for bands 0-5 (levels 1-59); higher bands show the nearest existing look. No armour equipped shows the band-0 Quilted Coat. |
| Active play | "Do all of them", 23 Sep 2026 (after the skill-timing options): a tapped skill is aimed (Burst goes to the toughest enemy) while auto-cast has no target logic (GDD section 4), and aimed bursts hit the Korstone or a boss for 500% (`LaneSim.AimedWeakPointPercent`). Measured 128-132% of auto-cast pace for a present player; the server pays it only for loops it replays. |
| Reference sheets | Chosen by the owner on 23 Sep 2026, all in `docs/concept/`: `vanguard-1`, `korstone-1`, `kestrel-1`, `wraithsworn-2`, `drumcaller-2`, `wolf-2`, `glow-1` (`-sheet.jpg`). The other variant of each is kept for comparison only. Every sheet after the Vanguard was generated with `vanguard-1` as the style reference; keep doing that for new sheets. |

## What exists and works

- `src/Orsuun.Rules`: engine-free rules (Forge, etchings/Turnstone, offline settlement, lane combat, zones, bosses,
  gear, XP/levels, item looks, active-play replay). 107 xUnit tests in `tests/`. Numbers pinned to the GDD (52.6 scrolls to +9, Turnstone odds, and so on).
- `src/Orsuun.Server`: ASP.NET Core 8 + PostgreSQL 16. Guest login, heartbeat settlement (live and offline), Forge,
  Turn, equip, park, push, boss fights with damage brackets, append-only ledger, idempotent request ids.
  `tools/smoke.ps1` walks every endpoint.
- `client/`: Unity 6000.0.32f1, URP 17 with the EmberGlow upgrade shader, everything built in code by `GameRoot`. Lane
  with the rigged Vanguard (armour and weapon looks per level band, Idle/Run/Attack/Hit/Death clips), 3D mobs, the
  Korstone model and zone backdrops; HUD, Forge / Gear / Zones / Shards screens in the direction-B palette with icons;
  push and boss replays from server seeds; seeded farm loops reported for active play; LOCAL MODE fallback.
- `tools/Orsuun.Sim`: Monte Carlo balance report. `tools/ClientCheck`: compiles Unity scripts with dotnet.

## Done since the first handoff (same day)

- Korshards and sockets (rules, server endpoints, SHARDS panel). Evening Bells and Bulk Turn.
- EF migrations (`Migrations/Initial`), Docker + Caddy deployment stack in `deploy/`, configurable server URL,
  `ProjectSetup.BuildAndroid` (needs the Android module; the Windows PC does not have it, the Mac should).
- Four art direction boards in GDD section 14 and `docs/art-options/`; recommendation B (modernized classic), D as
  the alternative. Owner picked B on 23 Sep 2026.

## Done 23-24 Sep 2026 (Mac)

- Machine setup, Hetzner playtest server, Android APK and iPhone installs. URP with the EmberGlow shader (classic MMO
  upgrade shine per piece). Item looks for bands 0-5, each armour rigged with five animations (`art/blender/rig.py`).
- Enemies in 3D: Hollowed Wolf, Hollowed Boar and Deserter (the Oathfields' three mobs), Old Greyjaw as a great wolf,
  Tul-Gorak and his captains as war-red deserters. Mobs bob while alive and keel over when slain.
- Active play wired end to end (see Known gaps for the rules): verified on 24 Sep 2026 with the Mac player against a
  local server, two loops reported and both replayed as exact matches.
- Wraithsworn and Drumcaller playable (sheets, Rodin, `looks.class_look`; the Drumcaller's staff is found as a
  straight line and pinned to her hand so it swings whole). Spell classes show magic on every hit: violet void bolts,
  lightning from the sky. Class switch on the Gear screen cycles all four; `-class <Name>` for local screenshots.
- Mobs attack visibly: the attacker lunges at the hero (bosses harder), red sparks on the hero, a sound per kind (bite,
  gore, clash, boss slam, void).
- Korstones rebuilt (`KorstoneLook`, `KorstoneFx`): tier colours through EmberGlow `_CrackRemap` (repaints the painted
  cracks), orbiting dark shards with glowing seams, rising embers, a pulsing ground glow and light, cracks that flare
  when struck, a spark ring on every wave and an awakening roar. Shapes in `Resources/Models/Korstones/A|B|C.fbx`;
  RenderPreview writes `artifacts/korstone-tiers.png` and `korstone-elders.png`.
- Kestrel playable (`HeroClass`, `SkillDef.For`, `HeroFactory` class shape, `Account.Class` + migration `HeroClass`,
  `POST /v1/class`); her model is rigged by `rig.rig_humanoid` (A-pose, hands measured, shared actions) through
  `looks.class_look` into `Resources/Models/Classes`. `KestrelTests` cover her kit, pace and loop replay (100 tests).
- Commanders in 3D: Tul-Gorak (an original steppe warlord; a first sheet read like Kratos from God of War and was
  dropped), the Mirage Queen (her images are her model washed violet) and Old Greyjaw, all via `looks.mob_model`.
- Hunt sound and feel: `GameAudio` (music crossfades, per-sound minimum gaps, SOUND toggle saved in PlayerPrefs),
  twelve lane sounds and two music tracks in `Resources/Audio` (Mirelo / Sonilo; the hunt loop is crossfaded end into
  start), hit sparks from one pooled particle system (`Resources/FxSpark.mat`, additive URP particles).
- Title and loading screen (`TitleScreen`, art in `Resources/Art/Title.jpg`, alternatives in `docs/concept/title-*`);
  the Unity splash is off and iOS shows the title art while loading. Dev switches: `-title`, `-notitle`, `-kestrel`.
- Gear screen rebuilt: worn pieces as tiles with rarity rims and +level badges, a detail card (own stats, what wearing
  it would change, etchings, sockets), the bag as a scrolling grid with a slot filter, and EQUIP / FORGE / TURN for any
  piece. Forge and Turn requests carry the item id (`ForgeRequest.ItemId`, `TurnRequest.ItemId`); upgraded bag pieces are
  never pushed out by the loot cap. Every Forge attempt asks first (`ConfirmDialog`). Dev switches for screenshots:
  `-sampleloot`, `-gear`, `-anvilbag`, `-confirm`.
- Forge outcome moment (`ForgeFx`): every attempt plays on its own layer over the Forge. The item sits in a bronze slot,
  the hammer falls two times (five from +7), then success flashes with rays in the new level's glow colour (+9 bigger),
  a lost level dulls and shakes the piece, the Anvil Ward rings blue, and an Oathbreak splits the icon into flying
  shards. Six sounds in `Resources/Audio/Forge*.wav`, generated with Mirelo (Higgsfield), trimmed to the hit and
  normalised; nobody has listened to them on a device yet. `-fxdemo Success|Nine|LevelLost|LevelKept|Oathbreak`
  plays the moment with a made-up result for screenshots.
- App icon: the blood-moon Korstone (`client/Assets/Orsuun/Art/AppIcon.png`, applied by `ProjectSetup.EnsureAppIcon`);
  the other three options are in `docs/concept/app-icon-*.jpg`. Android uses it as a legacy icon, no adaptive layers yet.
- UI colour pass in direction B: dusk-indigo panels, bronze rims, Philosopher Bold for buttons and headings, Cinzel for
  screen titles (both SIL OFL, licences next to the fonts), currency icons on the top bar, item names in the glow colours.

## Done 24 Sep 2026, later (store readiness and new zones)

- Class looks per band: `Resources/Models/Classes/<Class>_T0|T1|T2.fbx`, picked by the armour's band
  (`LaneView.SetHeroClass(cls, band)`, nearest band when one is missing); the old one-band models are gone. Each is
  split into `_Armor` and `_Weapon` so the two glow by their own items' levels, and the weapon rides its hand whole.
- Salt Flats (salt scorpion, glass snake, caravan ghoul) and Frost Pasture (frost bear, ice wight, snow hag) have their
  own mobs (`Resources/Models/Mobs`), backdrops and ground colours; Korstone Field III uses the Salt Flats set, IV-V the
  Frost set (`LaneView.MobSetFor`). RenderPreview writes `lane-mobs-SaltFlats.png` and `lane-mobs-FrostPasture.png`.
- First-session tutorial (`Tutorial`): eight steps with a pulsing frame (the hunt, the Korstone, skills, open the Forge,
  one attempt, back, Gear, Push/Zones); some wait for the player's action, SKIP ends it, stored in PlayerPrefs
  (`orsuun.tutorialDone`). `-tutorial` / `-tutorialStep <n>` for screenshots.
- MENU replaces the DEV button (`MenuPanel`): HOW TO PLAY, PRIVACY POLICY (opens `<server>/privacy`), DELETE ACCOUNT
  (confirm, `DELETE /v1/account`, then a new guest on a new device token), and the playtest grant (`ShowDevGrant`; the
  dev endpoints only exist while the server runs in Development). `-menu` opens it.
- Crash reports: uncaught client exceptions go to `POST /v1/client-log` (once per message, at most 10 a run; the server
  keeps 20 per account per hour in `ClientLogs`, migration `ClientLogs`). Read them with
  `docker compose -f deploy/docker-compose.yml exec db psql -U orsuun -d orsuun -c 'select * from "ClientLogs" order by "Utc" desc limit 20'`.
- Server abuse guard: 40 calls per 10 s per session (or IP before login), HTTP 429 above that; loopback exempt.
- Privacy policy page: `deploy/site/privacy.html`, served by Caddy at `/privacy`. Its contact line is a placeholder.

## Store release, waiting on the owner's accounts

- Apple Developer Program (paid) for TestFlight and the App Store, and a Google Play Console account for Play. Sign in
  with Apple / Google (so an account survives a new phone) needs both; guest login stays as the first step.
- A contact email for the privacy policy (replace "[contact email to be added before release]" in
  `deploy/site/privacy.html`) and the store listings.
- A production server: the playtest box runs in Development mode with the dev endpoints open. For release, run it with
  `ASPNETCORE_ENVIRONMENT=Production`, a real domain instead of sslip.io, and database backups; set `MenuPanel.ShowDevGrant`
  to false in store builds.
- App Attest / Play Integrity for one account per device (see Known gaps).

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
   Korshard icons, Banner emblems, Forge VFX boards, store screenshots) is the checklist in GDD section 14.1. Done since: bands 3-5
   of item looks, boar and deserter sheets and models, a first UI colour pass. Open: a Mirage Queen model (she and her
   images are still grey-box capsules), mob rigs (mobs move procedurally), bands 6-10 of looks, the other three classes
   in the lane.
3. Later: second class (Wraithsworn Voidpact), Bannerkin companion, sixth etching, Temper, Oath Renewal (GDD section 12),
   real shared boss HP pools, Hunt Marks and the Hearthfire subscription (Bulk Turn's 10/50 split depends on it).

## Known gaps

- `ORSUUN_RESET_DB=1` wipes the schema on a Development start; keep it out of any shared environment.
- The Wraithsworn's first band (the older Rodin model) carries two blades; bands 1-2 carry one sword and a void spell
  in the left hand. All three use the sword attack.
- The live lane's loot is display only; each heartbeat replaces it with the server's settlement.
- Active play: the account holds a lane seed (new on first login and every park) and the next loop number; StateDto
  carries both. Online, the client's farm lane runs one seeded loop per encounter cycle (`LaneSim.Cycles`; Hunting
  Grounds count a round of packs), records taps with their tick and queues the finished loop (`PlayerSession`). The
  heartbeat body carries the reports; the server replays each (`ActivePlay.Verify`, at most 20 per heartbeat, never more
  lane time than wall clock plus one loop) and pays the time-weighted pace, 100-135%, into the live settlement. A loop
  where auto-cast was toggled or the hero changed is not reported; a level-up between loop start and heartbeat makes that
  loop miss (paid at 100%). Offline time never earns the bonus. The ledger's settle-online rows show `efficiencyBp` and
  `loopsVerified`. Anyone who knows the rules can compute perfect play; that is capped at 135% by design.
- Boss damage ranks are against simulated rivals. New guest accounts are capped at 10 per network per day
  (client IP via X-Forwarded-For from Caddy, loopback exempt); real one-account-per-device needs App Attest / Play Integrity.
- Dev credentials (`orsuun` / `orsuun-dev`) are for local PostgreSQL only. The Hetzner box has its own random password in
  `/opt/orsuun/deploy/.env` (git-ignored).
- `appsettings.json` pins `Urls` to localhost:5080; in the container only the unprefixed `URLS` env var overrides it
  (`ASPNETCORE_URLS` loses to the JSON file). `deploy/docker-compose.yml` sets it.

## Blender pipeline (Mac, since 23 Sep 2026)

Blender 5.2 LTS is installed with the MCP for Blender addon (user config `~/.claude.json`, server `uvx mcp-for-blender`).
Start Blender, press N, BlenderMCP tab, Start MCP Server; Claude can then build and edit the open scene. First asset:
`art/blender/korstone-blockout.blend` (procedural Korstone with an emissive ember-crack material) exported to
`client/Assets/Orsuun/Models/Korstone_blockout.fbx`. Hyper3D Rodin and Hunyuan3D image-to-3D are available in the
addon panel; Rodin is enabled in fal.ai mode (key in `~/.config/fal/key`, about $0.40 per model). The MCP tool's fal.ai path
crashes before sending (it iterates the file-path list when given URLs), so submit to `https://queue.fal.run/fal-ai/hyper3d/rodin`
directly with front/side/back crops hosted under `/downloads`, `condition_mode: concat`, `tier: Regular`, `material: PBR`.
First result: the Vanguard (`art/blender/vanguard.blend`), decimated to 12k tris, in the lane as `Resources/Models/Vanguard.fbx`.
**fal.ai's balance ran out on 24 Sep 2026** (every Rodin call returns "Exhausted balance"; top it up to use Rodin
again). Since then 3D comes from Tripo H3.1 multiview through Higgsfield (`tripo_h3_1_multiview_to_3d`, about 9
credits a model, `face_limit: 40000`, front/side/back views in that order as imported media). Tripo models face +X:
pass `yaw_degrees=-90` to `class_look` / `mob_model`. Sources are kept as `art/blender/*-tripo.glb`.
Other classes go through `looks.class_look(glb, "<Class>_T<n>", height, weapon=..., attack=...)`: "knives" (a blade in
each hand; also the twin swords of the Wraithsworn's first band), "sword" (right hand) or "staff" (a straight line
through the right hand). Blades are found below each hand, then only the mesh island the hand grips is kept (boots and
cloth shreds beside a blade are separate islands); the weapon part is skinned whole to its hand. Heights: Kestrel 2.2,
Wraithsworn 2.3, Drumcaller 2.45.
Item looks (weapon and armour per level band) come from `art/blender/looks.py`: armour models are Rodin turnarounds of
the same man holding a glaive; the pipeline finds the glaive pole (RANSAC for the straight full-height line), cuts it out
and stores WeaponBase/WeaponTip; weapons are Rodin glaives normalised to base-at-origin. Output in
`Resources/Models/Looks` (FBX + texture) and `Resources/Looks` (materials). New band: sheet -> Rodin -> `armor_look` /
`weapon_look` -> RenderPreview. `pole_axis` only looks on the glaive hand's side (the Banner Lamellar carries a second,
straight banner pole on its back). `armor_look` also rigs each armour (`art/blender/rig.py`): one skeleton layout for the
shared sheet pose, hands measured per look (right from the pole, left behind the shield), distance-based skin weights
smoothed over the mesh, WeaponBase/WeaponTip parented to hand.R so the glaive follows the arm, and five actions keyed in
code (Idle, Run, Attack, Hit, Death; a positive rotation about a bone's local X swings its tip forward). Unity imports
them as legacy clips (`RenderingSetup.EnsureAnimatedImport`); RenderPreview writes `artifacts/pose-*.png`, baking the
skinned meshes because a one-shot editor render never runs the skinning step. Mobs: `looks.mob_model(glb, name,
height)` -> `Resources/Models/Mobs` (6k tris, facing -Y, feet on the ground). Every Vanguard armour look carries the
round shield from the chosen sheet; it is part of the class silhouette, not the shield item.

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
