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
| Turning helper | Owner, 24 Sep 2026 (after the classic bonus switcher): pick up to five etchings, each with the lowest tier accepted; it turns until all of them are on the piece; then "several pieces at once": up to eight pieces, worn or in the bag, each with its own goal and ON/OFF, turned in turn one batch each. Built as a stop rule of up to five targets (`TurnTarget`, `EtchingService.TurnUntil`, `TurnRequest.Targets`), all must match; the screen shows the exact chance per turn (`EtchingService.TargetChance`) and names goals that can never be met (`TargetProblem`). Assumption (not stated by the owner): the helper chains Bulk Turn batches of 50 until the goal, no Turnstones, or STOP, so the planned 10-free / 50-Hearthfire batch split would only change its speed. |
| Banners: red, blue, yellow | Owner, 24 Sep 2026: "make 3 banners: red blue and yellow, name and draw their flag according to our theme". The world bible's three creeds stay; the colours moved and two names changed to match: **Ember Banner** (crimson, "Break every stone", rider clans, Karsun), **Sky Banner** (blue, takes the monasteries' "Reseal what was sealed", Ostrakh), **Gold Banner** (yellow, takes the salt-road merchants' "Every stone has a price", Velimar). Flags in `docs/concept/banner-*.jpg`, cut-outs in `Resources/Art/Banners`. The oath is asked once, online, after the title screen; it cannot be changed yet (the bible's once-a-season defection is not built). |
| Multiplayer layer | Owner, 24 Sep 2026: "do all of them" (Banners, shared boss fights, fortress PvP). Built: Commander spawns have one HP pool for the server (sized by last week's fighters, at least one; the killing blow is named for its Banner; ranks count real fighters, simulated rivals fill to 20); the War of Banners point race per season; fortress sieges between Banners. Assumptions (not stated by the owner): a season is a week for the playtest; last season's winner hunts with +5% sorn and each fortress held gives +3%; until guilds exist the Banners hold the fortresses (the GDD has guilds bidding on Sunday 50v50 sieges); sieges are asynchronous scored fights, one per player every 10 minutes, attackers wear the wall down, defenders mend it by half their damage, the Hall's fall hands the fortress to the attacking Banner with the most siege damage; the GDD's "break each phase within 10 minutes" rule is not enforced yet. |
| Guilds | Owner, 24 Sep 2026: "create guild as well". Built from the GDD (guilds mix Banners, daily donation to the treasury, Guild Tallies for the guild shop, guild skills, guild flags on fortresses, 50 Tallies for a Commander's rank-1 guild). Assumptions (not stated by the owner): a charter costs 100,000 sorn; 20 members, +5 per Muster level (max 40); up to 200,000 sorn donated per member per bounty day, 1 guild XP per 1,000 sorn and 1 Guild Tally per 5,000; levels 1-10; skills are bought from the treasury by the leader or an officer (the GDD lists Tallies as the skill currency, the treasury felt clearer): Plunder +1% hunting sorn per level (max 5), Muster; guild shop: Anvil Ward 30, Khan's Alloy 40, Trooper Korshard 8 Tallies; the 50 Tallies go to the rank-1 fighter (in a guild) when the spawn falls, with 50 guild XP; siege damage gives guild XP (1 per 10,000); the member who breaks a Hall for the conquering Banner raises the guild's flag there (+2% sorn per flag); guilds are open or shut (no join requests yet); leaders promote (4 officers max), demote, hand over the lead; officers remove members; a leader leaving passes the lead to the highest rank that stayed longest, the last member leaving disbands. Guild war and fortress bids are not built. |
| Chat | Owner, 24 Sep 2026: "all chat". A world channel for everyone and one channel per guild; guild events are system lines in guild chat (the guild log); Commander kills, fortress captures and +8 or better forges are system lines in world chat. Assumptions: 200 characters a line, one line per 3 s, bad words starred out (`WordFilter`), three reports hide a line, players can block others (unblock all from the chat screen), lines are kept 7 days. The newest world line runs over the bottom of the lane (tap it for CHAT). |
| Salt Exchange | Owner, 24 Sep 2026: "a global trading screen that all players can list their items for gold or buying from them". Built as the GDD's Salt Exchange for gear: any bag piece listed for sorn; buyers pay the price, the seller gets it less the GDD's 5% tax. Assumptions: 1,000 to 1,000,000,000 sorn, 10 listings a player, 48 hours then the piece goes back; worn pieces must be taken off first; a listed piece cannot be worn, forged or turned; BUY pages by slot and order (cheapest, newest, highest +). Materials, shards and consumables are not tradable yet. |
| Accounts | Owner, 24 Sep 2026: "a sign up sign in screen". Email and password (PBKDF2-SHA256, 210,000 iterations); CREATE ACCOUNT saves them to the hero being played; SIGN IN points this phone at an account (a guest hero with progress is warned first); SIGN OUT starts a new guest; sessions are per device, so one hero can be played on two phones. Shown once after the title screen on a guest's first launch (then from MENU, ACCOUNT). Assumptions: 8+ character passwords, 8 wrong tries per email per 15 minutes; no email verification or password reset yet (no mail sending). |
| Sign in with Apple / Google | Owner, 24 Sep 2026: "i want to add sign in through google and apple". Built as a browser sign-in for both, on both platforms: the game asks the server for the provider's page, opens it in Apple's in-app sign-in sheet (iOS, `Plugins/iOS/OrsuunAuth.mm`) or the browser (Android), the provider returns to the server, which checks the signed ID token against the provider's published keys and sends the app a one-time ticket on an `orsuun://auth` link; the ticket only works on the device that started. A login already linked to a hero switches the phone to it; a new one is linked to the hero being played. Needs the owner's keys (see "Store release"); until then the buttons stay hidden. The Development server's stand-in provider ("dev", MENU > DEV: TEST SIGN-IN) walks the same round trip. A native iOS Apple button (no sheet) can come later: the server already takes ID tokens at `/v1/auth/external`. |
| Moderation | Owner, 24 Sep 2026 ("go" on the moderation tool before inviting testers). A web page at `/admin` (served by the game server) for moderators: game accounts whose email is in `Admin:Emails` (`ADMIN_EMAILS` in the server's `deploy/.env`; the owner's is set). Sign in with that account's email and password (12-hour session). Tabs: overview, the report queue (hide, keep, all lines of a player), world chat search, players (mute 1 h / 24 h / 7 days, ban with a reason the player sees, unban), guilds (rename, disband), and the moderation log. A ban blocks sign-in, closes their Exchange listings, takes them out of their guild and hides their lines. |
| Bounties and Hunt Marks | Owner, 24 Sep 2026 ("do all of them"; the GDD's Hunt Marks). Five daily and four weekly bounties counted by the server (Korstones, hunting minutes, forges, turns, Commander fights, pushes, sieges), reset at 20:00 server time (weekly on Mondays); the Hunt Marks shop sells Etching Needles, Pinning Wax, Turnstones, Scrolls of Mercy and Draughts. ETCH (Etching Needle, 1st to 4th etching at 100/80/60/40%) and PIN (Pinning Wax, one lock per item, turns cost two, unpinning spends the wax) are on the Forge. The owner will add monetization; the shop prices are placeholders. |
| Guild war and fortress bids | Owner, 24 Sep 2026 (desktop app session): picked all four offered next steps, among them "guild war and fortress bids". Built asynchronous like the sieges (the GDD's live 20v20 and 50v50 are out of reach for now). Guild war: the leader signs up (3+ members), war nights Wednesday and Saturday 21:00 pair guilds by Elo rating for an hour; each member fights up to 6 duels, 2 min apart, on one of three lanes against a drawn member of the other guild; a win is a kill and pushes the lane (two steps under the war flag the leader or an officer plants), 5 steps break it; score = kills + 10 per broken lane; winner 150,000 treasury sorn + 100 guild XP (draw 50,000 + 60, loss 30 XP); a duel pays 5,000 sorn + 1 Hunt Mark (GDD: PvP pays currency, never upgrade protection). Duels follow the GDD's PvP balance: gear and level on a class-neutral frame, stats above the pair's median compressed by 30%, a seeded roll tuned so a +9 set beats a +7 set about 80% of the time (`Rules/GuildWar.cs`: `GuildWars`, `Duels`). Fortress keeps: the Banner sieges stay; the keep decides the guild flag. Leaders or officers bid treasury sorn on one keep a week (50,000+); Sunday 20:00 the top four bids contend (spent, the rest refunded) and storm the keep for an hour while its holders mend it; the best contender takes it past 150,000 + the mending. The holder flies its flag (+2% sorn) and earns 2% of the Exchange tax. All numbers are assumptions (not stated by the owner). |
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

## Where we left off (24 Sep 2026, desktop app session, late)

The owner moved to the Claude desktop app (Code tab, `~/orsuun`) and picked all four offered next steps. All four are
built and committed on `main` locally: the first ten minutes (spotlight tutorial, next-goal line), the rest of the
mockup look, the Mirage Queen (she already had a real rigged model; only the notes were stale), and guild war with
fortress bids (see the decision row and "Done ... desktop app session"). Pushed to GitHub; build 26092419 is installed
on the owner's iPhone (24 Sep 2026, 22:34); the playtest server is deployed (22:50, migration `GuildWarKeeps` applied)
with a home page at `/` (`deploy/site/index.html`, routed in the Caddyfile; a single-file bind mount keeps the old
Caddyfile after `git pull`, so restart the caddy container after changing it). A copy of the playtest database from
just before that deploy is on the Mac: `~/orsuun-backups/playtest-before-guildwar-2026-09-24.sql.gz`. Google sign-in is
published (owner, 24 Sep 2026: "published"); its Branding page wants the home page `https://65.108.221.210.sslip.io/`.
The Android APK of this session is on the download link (24 Sep 2026, 22:56); the previous one is kept beside it as
`/opt/orsuun/downloads/Orsuun-prev.apk`.

Waiting on the owner: database backups (yes/no, Storage Box or Mac); the
paid Apple Developer Program (TestFlight, Sign in with Apple, no 7-day expiry); Google sign-in test users or "Publish
app" in Google Cloud; the monetization plan; a real domain before release.

Could come next: the Mirage Queen's presence (a mirage shimmer, ghostlier images, a little taller), Free Lances and the
fortress aura for keeps, guild invites, password reset by email (needs a mail service), bands 6-10 of looks.

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
- App icon: owner's pick 24 Sep 2026 is "Shatter", the Korstone bursting apart in white-gold light
  (`client/Assets/Orsuun/Art/AppIcon.png`, applied by `ProjectSetup.EnsureAppIcon`; source
  `docs/concept/app-icon-v2-shatter.jpg`). Keep the other options: the first blood-moon icon and three more in
  `docs/concept/app-icon-*.jpg`, ten Korstone palettes in `app-icon-v2-*.jpg` (sheet: `app-icon-v2-options.jpg`).
  Android uses it as a legacy icon, no adaptive layers yet.
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

- Turning helper (`TurnHelperPanel`, from the Forge's TURNING HELPER button; TURN x1 stays on the Forge, the x10/x50
  buttons and the one-etching stop rule are gone): a strip of up to eight pieces (+ adds any owned piece with
  etchings; the anvil piece joins on open), and for the selected one ON/OFF, REMOVE, its etchings with goal lines in
  green/amber, five goal rows (etching picker with each entry's T1/T5 values, tier T1+..T5+ capped by rarity), odds per
  turn and Turnstones on average. START turns every piece that is ON, one 50-turn batch each in turn, marking them
  DONE / CHECK (goal out of reach) / GONE, until all are done, the Turnstones run out, or STOP. Pieces turn where they
  are (`PlayerSession.TurnBulk(item, ...)`; online by item id), never via the anvil. Online the helper's pieces, goals
  and ON/OFF survive a restart (PlayerPrefs `orsuun.turnQueue`, by server id); a new piece starts from the last goal
  set for its pool (weapon, other slots). The server still takes the older `StopEntryId`/`MinTier` from installed
  clients. `-turnhelper pick|add|demo|run` for screenshots (run: three pieces, 600 local Turnstones, START).

## Done 24 Sep 2026, evening (War of Banners and the rest)

- Banners, the oath screen (`BannerOath`), the WAR screen (`WarPanel`: standings, last winner and bonus, the three
  fortresses with ATTACK / DEFEND and the cooldown), sieges replayed from seeds (`GameRoot.FightSiege`; champions:
  Gate Warden = Ice Wight, Yard Captain = steel Deserter, Lord of the Hall = dark Tul-Gorak). Server:
  `GameService.War.cs` (oath, points upsert, bonus, row-locked Commander pools and sieges), migration `WarOfBanners`,
  fortresses seeded at startup. Rules: `Banners.cs` (Banners, points, generated player names like "Swift Falcon 4821",
  fortresses and champions), `BossRun.RankShared`.
- Bounties (`Bounties.cs`, `BountyPanel`, server `GameService.Bounties.cs`), the shop, ETCH and PIN.
- HUD bottom row: ZONES, WAR, BOUNTIES (marked when one is ready), SPEED (GUILD since the guild round), MENU; SOUND moved into MENU; the Banner's
  flag stands in the lane's top-right corner (tap for WAR). ZONES shows each Commander's server pool and best fighter.
- Local notifications (`GameNotifications`, Unity Mobile Notifications 2.4.0, no push server): the full offline hunt,
  the next Evening Bell, the next Commander and new bounties are scheduled when the game goes to the background; the
  permission is asked once after the oath.
- Kestrel, Wraithsworn and Drumcaller looks 4-6 (item levels 30-59; sheets `docs/concept/<class>-T3..T5-sheet.jpg`).
- Every enemy is rigged with Idle / Run / Attack / Hit / Death (`art/blender/mobrig.py`, through
  `looks.mob_model(..., rig=plan)`: biped for people and the undead, quadruped for wolves, boars, Greyjaw and the bear,
  serpent for the glass snake, scorpion for the scorpion); LaneView plays Run while they close in, Idle in place,
  Attack on their blow, Hit when struck and Death instead of the old keel-over. Heights: Wolf 1.25, Boar 1.15,
  Deserter 1.9, Greyjaw 2.0, Gorak 2.45, Queen 2.4, Scorpion 1.0, GlassSnake 1.3, Ghoul 1.85, FrostBear 1.6,
  IceWight 1.9, SnowHag 2.0 (Tripo sources need `yaw_degrees=-90`).
- Screenshot switches: `-oath`, `-war`, `-bounties`, `-guild` (online screenshots need `-server <url>` instead of `-local`).
- Banner flags normalised (owner: "banners sizes arent equal"): the three cut-outs share one 266x698 canvas with equal
  cloth height, crossbars on one line and pole feet on one line (Sky's and Gold's poles shortened, Ember's lengthened);
  `docs/concept/banner-*.jpg` keep the full art.
- Guilds (`Guilds.cs`, `GuildPanel`, server `GameService.Guilds.cs`, migration `Guilds`, endpoints under `/v1/guild`):
  browse and search, found (name, 2-4 letter tag, 8 colours, basic word filter), join, leave, ranks, donate, skills,
  shop, open or shut gates. The GUILD button replaced SPEED on the HUD (SPEED is in MENU as HUNT SPEED); the guild tag
  shows under the lane flag, on Commander boards ("[TAG] Name") and on fortresses that fly the guild's flag. Guild rows
  change under a row lock (`LockGuildAsync`); other members' rows through single UPDATEs.

## Done 24 Sep 2026, night (chat, guild requests, the Salt Exchange, accounts)

- Rules: `WordFilter` (guild names and chat), `Chat`, `Market`, `AccountRules`; tests in `SocialTests`.
- Server: `GameService.Chat.cs` (channels, system lines, reports, blocks), guild join requests (`GuildRequest`, shut gates
  take an ASK, `/v1/guild/answer`), `GameService.Market.cs` (listings locked for buy and cancel, lazy expiry on market
  reads and every `/me` and heartbeat), `GameService.Auth.cs` (`Device` rows hold device tokens and sessions,
  `/v1/auth/login`, `/v1/auth/register`, `/v1/auth/signout`), migration `ChatMarketAccounts`. Account deletion also
  removes devices, chat lines and reports, guild requests and listings.
- Client: `ChatPanel` (scroll list, WORLD / GUILD, report and block, the lane ticker), `MarketPanel` (BUY / SELL /
  MY LISTINGS), `AccountPanel` (create, sign in, guest, signed in with SIGN OUT), GUILD CHAT and ASKING (n) on the
  guild screen. HUD bottom row: ZONES, WAR, BOUNTIES, GUILD, TRADE, MENU. MENU gained ACCOUNT.
- `tools/smoke-social.sh [url]` walks chat, requests, the Exchange and sign in with its own accounts and deletes them
  (safe against the live server). Screenshot switches: `-chat`, `-market`, `-account`.
- Privacy policy: contact uardilbaran@gmail.com; now covers emails, chat and trades.

## Done 24 Sep 2026, late night (UI upgrade)

- Owner: "user ui is bad. upgrade it". A UI kit in direction B, drawn by `tools/ui/make_ui_kit.py` into
  `Resources/UI` (bevelled button plates under studded bronze rims, cards with corner diamonds, round skill rings and a
  cooldown disc, bar troughs, currency pills, the crimson title ribbon, top and bottom bars, badge, the screen backdrop).
  `Ui.Kit` nine-slices them at runtime (borders in `Kit.Borders`, drawn at 2x). Painted icons from two sheets
  (`docs/concept/icons/nav-sheet.jpg`, `skills-sheet.jpg`): `Resources/Icons/Nav*.png` (cut out) and
  `Resources/Icons/Skills/<letters of the skill name>.png` (round).
- Every screen picks the kit up through `Ui.Button`, `Ui.Framed`, `Ui.Title` (carved titles at the top stand on the
  ribbon) and `Ui.Backdrop` (opaque, fades the screen in). New helpers: `Ui.Bar`, `Ui.RoundButton`, `Ui.NavButton`,
  `Ui.IconButton`, `Ui.Sliced`; buttons sink under the finger (`Press`).
- HUD rebuilt: level medallion and currency pills on the top bar, a shade behind the stage line, HP in a bronze
  trough, a hero plate with the weapon, round skill buttons with painted icons, a cooldown sweep and seconds, AUTO
  switches under them, icon buttons for FORGE / GEAR / SHARDS / PUSH, and a bottom bar of painted icons with a red
  badge when a bounty is ready.

## Done 24 Sep 2026, late night (moderation)

- `GameService.Admin.cs`, `src/Orsuun.Server/Admin/admin.html` and `admin.js` (embedded in the server; every
  player's words go in with `textContent`; the page sends a strict Content-Security-Policy), migration `Moderation`
  (`Account.MutedUntilUtc`, `BannedUtc`, `BanReason`, `ChatMessage.Reviewed`, `AdminAction`). Muted players get
  "A moderator muted you for N more minutes" in chat; banned ones get "This account is banned: reason" at sign-in.
  A new report on a line a moderator already looked at puts it back in the queue.
- `tools/smoke-admin.sh [url]` needs a server started with `Admin__Emails=mod-smoke@example.com` (local only).
- To add a moderator: append their account email to `ADMIN_EMAILS` in `/opt/orsuun/deploy/.env` and restart the
  stack (`docker compose ... up -d`).

## Done 24 Sep 2026, night (sign in with Apple / Google)

- Server: `Game/ExternalAuth.cs` (flows and tickets in memory, OIDC key discovery, Google code exchange with PKCE,
  Apple form_post), `LinkOrLoginAsync` in `GameService.Auth.cs`, `ExternalLogin` table (migration `ExternalLogins`),
  endpoints `/v1/auth/providers`, `/v1/auth/external/begin`, `/auth/{provider}/start`, `/auth/google/callback`,
  `/auth/apple/callback` (POST), `/v1/auth/ticket`, `/v1/auth/external`. Settings in `deploy/.env`:
  `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `APPLE_SERVICES_ID` (empty = not offered).
- Client: `ServerLink.BeginExternal` / `OnAuthCallback` (the iOS sheet's result and Android deep links), the account
  screen's CONTINUE WITH APPLE / GOOGLE and ALSO LINK buttons, `Editor/AuthBuild.cs` (iOS: AuthenticationServices and
  the orsuun scheme; Android: the orsuun://auth intent filter on the launcher activity).
- `tools/smoke-external.sh [url]` walks it with the dev provider.

## Done 24 Sep 2026, night (painted screens)

- Owner: scenes for every screen with menus, "do it directly [in the] ui". Twelve painted portrait scenes in direction
  B (`docs/concept/screens/bg-*.jpg`, in the game as `Resources/Scenes/*.jpg`): Forge, Gear, Shards, Turning, Zones,
  War, Bounties, Guild, Exchange, Chat, Gate (MENU and the account screen), Oath. `Ui.Backdrop(canvas, "Name")` lays the
  scene under a shade that darkens toward the bottom. `Editor/SceneArtImport.cs` keeps their 752x1344 size.
- AI mockups of whole screens (`docs/concept/screens/mockup-*.jpg`: hunt, forge, gear, korshards, turning, zones, war)
  are a design target for a further UI pass: filigree headers, painted thumbnails on zone and fortress cards, framed
  item icons.

## Done 24 Sep 2026, night (the mockups' assets in the game)

- Owner: "get all of the assets inside these [AI mockups] and integrate to our game ... make assets separately". Each
  piece was painted on its own by Higgsfield with a mockup as reference (`docs/concept/ui-kit/*.png`): title banner,
  panel frame, button, back button, section header, slot frame, bar frame, skill ring, level medallion, three
  fortress paintings. `tools/ui/cut_ai_kit.py` cuts them out (black background flooded away, button split into a
  tintable lacquer plate and its gold frame, frame centres cut out, stretch centres flattened) into `Resources/UI`
  over the procedural kit, with nine-slice borders in `Resources/UI/Borders.json` (read by `Ui.Kit`). Run
  `make_ui_kit.py` first, then `cut_ai_kit.py`.
- In the game: every button, card, title and bar; `BACK TO ...` buttons use the arrow-tipped plate; `Ui.Section`
  headers (Commanders, Fortresses, Hunt Marks shop); `Ui.Picture` framed pictures from `Resources/Thumbs` (zone
  thumbnails cropped from the environment art, Commander portraits cropped from their sheets, fortress paintings
  behind the War cards); Gear tiles are gold slots with a rarity glow; the HUD level sits on the medallion.

## Done 24 Sep 2026, desktop app session

- First ten minutes. The tutorial (`Tutorial`) now dims the screen around its target (four shades that take no taps),
  frames it in the gold slot rim with a pulsing glow, and points at it with a bobbing bronze arrow
  (`Resources/UI/Pointer.png`, drawn by `make_ui_kit.py`). Each note has a title. Ten steps: two new ones show the
  bottom bar (WAR, BOUNTIES, GUILD, TRADE, MENU) and the goal line. It also hides behind the Guild, Exchange, Chat
  and account screens now (they sit below its canvas).
- Next goal line (`Rules/Goals.cs`, `GoalTests`; `Hud.UpdateGoal`): a framed plate at the top of the lane shows one
  goal at a time; tapping it opens the Forge, Gear, Bounties or Guild, or lights the PUSH button. Reminders first (a
  finished bounty, an empty slot with a piece for it in the bag), then a chain: weapon +1, clear stage 1, gear in 3
  slots, clear stage 3, any worn piece +3, level 10, clear stage 5, join a guild (online only), gear in all 8 slots,
  clear all ten stages, weapon +7, weapon +9. Each clear names the zones it opens. A met chain step stays met
  (the furthest step is kept per account in PlayerPrefs `orsuun.goalStep.<player name>`, `local` offline); one met
  while playing flashes GOAL MET with the level-up sound. Assumption (not stated by the owner): guidance only, no
  rewards; a reward per goal would need the server to count them.

- The rest of the mockup look (`docs/concept/screens/mockup-*.jpg`). Hunt: FORGE / GEAR / SHARDS / PUSH are square
  lacquer tiles with big icons (`Ui.Tile`), skills stand in a framed panel with AUTO lamps, the hero plate shows the
  weapon in a gold slot, the bottom bar is six framed tiles (`Ui.SlotTile`), the loot log floats over the lane.
  Forge: framed slot row (the piece on the anvil lit gold), a big picture of the piece (`ItemPreview`: an offscreen
  camera at y = -400 renders the real weapon or armour look for its band, glowing by its level; the Vanguard's glaive
  lies across it, other classes show their whole model with only that piece glowing; stat-only slots show their
  painted icon), a name plate, an ETCHINGS card with tier colours, the attempt in a pill, method tiles with icons.
  Korshards (`SocketPanel`, rebuilt): the piece in a card with its sockets as gem slots, SHARDS rows per rank with
  painted gems (`Icons/Shard<Rank>.png`, sheet `docs/concept/icons/korshards-ranks.png`, GPT Image 2.5 on Higgsfield
  with a transparent background, cut by `tools/ui/cut_icons.py`), a trait picker and one SET / CLEAR button; `-shards`
  opens it. Gear: a big framed picture in the detail card. Zones: a scrolling list of big cards with HUNT HERE /
  HUNTING NOW / LOCKED (the Gorak War Camp row was missing before: 9 rows for 10 entries). War: three Banner columns
  lacquered in their colours with big flags, three tall fortress tiles with their paintings. Turning helper: slot
  tiles with rarity glow, the piece's picture, the goals in a card with numbered rings and red X buttons.

- Guild war and fortress keeps. Rules: `GuildWar.cs` (`GuildWars` schedule, pairing, fronts, score, Elo; `Duels` neutral
  frame, compression, edge, roll, and `Stage`, which shapes the replay's champion so the attacker's own hero wins or
  falls as decided; `FortressKeeps`), `GuildWarTests`. Server: `GameService.GuildWar.cs`, `GameService.Keeps.cs`,
  `WorldClock` (a hosted service: every 30 s it pairs a night that has begun, settles wars whose hour is up and moves each
  keep through its week), migration `GuildWarKeeps` (`GuildWarSignups`, `GuildWarNights`, `GuildWars`,
  `GuildWarEntries`, `FortressBids`; `Guild.WarRating/WarWins/WarLosses/WarDraws`; `Fortress.Keep*`). Endpoints
  `GET /v1/guild/war`, `POST /v1/guild/war/signup|flag|fight`, `POST /v1/keep/bid|fight`; `/v1/war` carries the keeps.
  The Banner Hall capture no longer moves the guild flag. Dev (Development only): `/v1/dev/war-night?minutes=`,
  `/v1/dev/war-end`, `/v1/dev/keep-siege?minutes=`, `/v1/dev/keep-end`; `tools/smoke-war.sh` walks it all (local only:
  it changes ratings and keeps). Client: `GuildWarPanel` (GUILD WAR from the guild screen: record, next night and SIGN
  UP, the score, three lanes with fronts, flags, FIGHT HERE / PLANT FLAG, the ladder), the WAR screen's KEEP buttons
  and keep dialog (holder, bids, BID +50,000 / +250,000 / +1,000,000 after a confirm, STORM / HOLD THE KEEP),
  `GameRoot.FightDuel` / `FightKeep` replays (a rival shows in their class look for their band; a Vanguard rival as a
  steel deserter). Screenshot switches online: `-guildwar`, `-duel <lane>`, `-keep <n>`; `-boss <id>` fights a
  Commander in local play.

## Store release, waiting on the owner's accounts

- Apple Developer Program (paid) for TestFlight and the App Store, and a Google Play Console account for Play. Sign in
  with Apple / Google (so an account survives a new phone) needs both; guest login stays as the first step.
- Google sign-in: published on 24 Sep 2026 (the consent screen is "In production", anyone can sign in); set up the same
  day for the playtest server (Google Cloud project "Orsuun", Web application client,
  redirect `https://65.108.221.210.sslip.io/auth/google/callback`; the key file is on the Mac at
  `~/.config/orsuun/google-oauth.json`, never in git; id and secret are in the server's `deploy/.env`). A new server domain needs a new redirect URI there.
- Apple sign-in: the paid Apple Developer Program; an App ID `com.orsuun.warofbanners` with Sign in with Apple, a
  Services ID (e.g. `com.orsuun.warofbanners.signin`) with Sign in with Apple configured for the server's domain and
  return URL `https://<server>/auth/apple/callback`; put the Services ID in `APPLE_SERVICES_ID`. Apple's review asks
  for Sign in with Apple whenever Google sign-in is offered.
- A contact email for the store listings. The privacy policy uses uardilbaran@gmail.com (owner, 24 Sep 2026: "for now").
- A production server: the playtest box runs in Development mode with the dev endpoints open. For release, run it with
  `ASPNETCORE_ENVIRONMENT=Production`, a real domain instead of sslip.io, and database backups; set `MenuPanel.ShowDevGrant`
  to false in store builds.
- App Attest / Play Integrity for one account per device (see Known gaps).

## Next steps, waiting on the owner

0. Database backups (owner, 24 Sep 2026: "not now keep that in mind"): nothing backs up the playtest database yet. The
   plan to offer: a nightly `pg_dump` kept 14 days on a Hetzner Storage Box (about 4 EUR a month) or pulled to the Mac.
   Do it before real players arrive.

0. Monetization in the turning helper: the owner will add it and give the details (24 Sep 2026: "we will add
   monetization here, I will let u know"). Build nothing for it until then; the Hearthfire 10/50 batch split noted
   under "Turning helper" belongs to that decision.

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
   of item looks, boar and deserter sheets and models, a first UI colour pass. Open: bands 6-10 of looks. (The Mirage
   Queen, mob rigs and the other three classes are done: see the sections above.)
3. Later: second class (Wraithsworn Voidpact), Bannerkin companion, sixth etching, Temper, Oath Renewal (GDD section 12),
   real shared boss HP pools, Hunt Marks and the Hearthfire subscription (Bulk Turn's 10/50 split depends on it).

## Known gaps

- `ORSUUN_RESET_DB=1` wipes the schema on a Development start; keep it out of any shared environment.
- `tools/smoke.sh` against the live server swears a Banner, sieges Stagfort and scores points in the shared world.
  Reset after it: `DELETE FROM "BannerScores"; UPDATE "Fortresses" SET "Wall"="WallMax", "SiegeEmber"=0, "SiegeSky"=0, "SiegeGold"=0;`
  (through `docker compose -f deploy/docker-compose.yml --env-file deploy/.env exec -T postgres psql -U orsuun -d orsuun -c '...'`).
- The fifth etching needs a Master's Needle, which nothing sells or drops yet (it is a Caravan item in the GDD).
- Players are shown to each other by a generated name; custom names need moderation first. Guild names and tags go
  through a short word filter (`Guilds.Clean`) only; reports and a review queue are needed before a public launch.
- Guilds: no invites. Guild war and fortress keeps are asynchronous first versions (no live 20v20 / 50v50, no Free
  Lances, no fortress aura, no class bonuses at gates); the treasury buys skills and keep bids.
- Accounts: no email verification and no password reset (the server sends no mail yet); a forgotten password means a
  lost account until that exists. Sign in with Apple / Google still needs the owner's developer accounts.
- Moderation has no alert for new reports (check the /admin overview), bans are per account (a banned player can
  start a new guest, within the 10-accounts-per-network-a-day cap), and admin sessions live in server memory (a
  restart signs moderators out).
- The Salt Exchange trades gear only, and a seller learns of a sale only in MY LISTINGS (no notification).
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
fal.ai's balance ran out on 24 Sep 2026 ("Exhausted balance" on every call); the owner's top-up that day covered one
Regular Rodin job and it is empty again. Rodin in concat mode twice gave the Wraithsworn a second blade in his empty
left hand (it seems to read the back view's sword as the other hand's); Tripo read the same three views correctly,
so the Wraithsworn's bands all come from Tripo (sheets in `docs/concept/<class>-T<n>-sheet.jpg`). The class bands and the Salt Flats / Frost Pasture mobs made in between come from Tripo H3.1 multiview through
Higgsfield (`tripo_h3_1_multiview_to_3d`, about 9 credits a model, `face_limit: 40000`, front/side/back views in that
order as imported media), which held up as well as Rodin; either works. Tripo models face +X: pass
`yaw_degrees=-90` to `class_look` / `mob_model`. Sources are kept as `art/blender/*-tripo.glb`.
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
