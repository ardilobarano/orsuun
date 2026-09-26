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
| All four classes, mob attacks, Korstone tiers | Owner, 24 Sep 2026. Wraithsworn (Voidpact: Void Lance 750%/8 s, Grave Tide, Pact Frenzy; 105% attack, 60% defense, 80% HP, 13-tick swings, weak point 700) and Drumcaller (Thunder Rite: Sky Hammer 450%/7 s, Storm Drum 200%/8 s, War Rhythm 7 s; 95/110/105%, +5% crit) join Vanguard and Kestrel; all four push within 10% of each other and pay 123-135% for aimed play (`ClassBalanceTests`). Korstones change with level: five tiers of 20 levels (Ember, Blood, Void, Grave, Khan colours, darker stone each tier) and three shapes (runed monolith, chained twin spire, crowned obelisk; an Elder takes the next shape up). 25 Sep 2026 (owner: "balance"): Kestrel 90/90/95, Wraithsworn 100/95/90, Drumcaller 85/100/100 so each needs about the Vanguard's forge level at the late map bosses (see "Done 25 Sep 2026"). |
| Outfits, Wraithsworn sword, class bands | Owner, 24 Sep 2026: the women wear the shortest shorts with garters (Kestrel and the Drumcaller: shorts, garter straps, thigh-high stockings), still clothed and non-explicit. The Drumcaller's first band keeps a mid-thigh tunic over them because the image generator refused the bare version; the filter was not worked around. The Wraithsworn attacks with the Kestrel's slash rhythm but with a sword (`rig.ATTACKS["sword"]`: wind-up overhead, slash with a chest twist). Kestrel, Wraithsworn and Drumcaller now have three looks each (T0-T2, every 10 item levels like the Vanguard). Assumption (not stated by the owner): their weapon look follows the armour's band (the blade or staff is part of the class model), while its glow still follows the weapon's own level. |
| Turning helper | Owner, 24 Sep 2026 (after the classic bonus switcher): pick up to five etchings, each with the lowest tier accepted; it turns until all of them are on the piece; then "several pieces at once": up to eight pieces, worn or in the bag, each with its own goal and ON/OFF, turned in turn one batch each. Built as a stop rule of up to five targets (`TurnTarget`, `EtchingService.TurnUntil`, `TurnRequest.Targets`), all must match; the screen shows the exact chance per turn (`EtchingService.TargetChance`) and names goals that can never be met (`TargetProblem`). Assumption (not stated by the owner): the helper chains Bulk Turn batches of 50 until the goal, no Turnstones, or STOP, so the planned 10-free / 50-Hearthfire batch split would only change its speed. |
| Banners: red, blue, yellow | Owner, 24 Sep 2026: "make 3 banners: red blue and yellow, name and draw their flag according to our theme". The world bible's three creeds stay; the colours moved and two names changed to match: **Ember Banner** (crimson, "Break every stone", rider clans, Karsun), **Sky Banner** (blue, takes the monasteries' "Reseal what was sealed", Ostrakh), **Gold Banner** (yellow, takes the salt-road merchants' "Every stone has a price", Velimar). Flags in `docs/concept/banner-*.jpg`, cut-outs in `Resources/Art/Banners`. The oath is asked once per account, at the character screen before the first hero (see "The way in"); it cannot be changed yet (the bible's once-a-season defection is not built). |
| Multiplayer layer | Owner, 24 Sep 2026: "do all of them" (Banners, shared boss fights, fortress PvP). Built: Commander spawns have one HP pool for the server (sized by last week's fighters, at least one; the killing blow is named for its Banner; ranks count real fighters, simulated rivals fill to 20); the War of Banners point race per season; fortress sieges between Banners. Assumptions (not stated by the owner): a season is a week for the playtest; last season's winner hunts with +5% sorn and each fortress held gives +3%; until guilds exist the Banners hold the fortresses (the GDD has guilds bidding on Sunday 50v50 sieges); sieges are asynchronous scored fights, one per player every 10 minutes, attackers wear the wall down, defenders mend it by half their damage, the Hall's fall hands the fortress to the attacking Banner with the most siege damage; the GDD's "break each phase within 10 minutes" rule is not enforced yet. |
| Guilds | Owner, 24 Sep 2026: "create guild as well". Built from the GDD (guilds mix Banners, daily donation to the treasury, Guild Tallies for the guild shop, guild skills, guild flags on fortresses, 50 Tallies for a Commander's rank-1 guild). Assumptions (not stated by the owner): a charter costs 100,000 sorn; 20 members, +5 per Muster level (max 40); up to 200,000 sorn donated per member per bounty day, 1 guild XP per 1,000 sorn and 1 Guild Tally per 5,000; levels 1-10; skills are bought from the treasury by the leader or an officer (the GDD lists Tallies as the skill currency, the treasury felt clearer): Plunder +1% hunting sorn per level (max 5), Muster; guild shop: Anvil Ward 30, Khan's Alloy 40, Trooper Korshard 8 Tallies; the 50 Tallies go to the rank-1 fighter (in a guild) when the spawn falls, with 50 guild XP; siege damage gives guild XP (1 per 10,000); the member who breaks a Hall for the conquering Banner raises the guild's flag there (+2% sorn per flag); guilds are open or shut (no join requests yet); leaders promote (4 officers max), demote, hand over the lead; officers remove members; a leader leaving passes the lead to the highest rank that stayed longest, the last member leaving disbands. Guild war and fortress bids are not built. |
| Chat | Owner, 24 Sep 2026: "all chat". A world channel for everyone and one channel per guild; guild events are system lines in guild chat (the guild log); Commander kills, fortress captures and +8 or better forges are system lines in world chat. Assumptions: 200 characters a line, one line per 3 s, bad words starred out (`WordFilter`), three reports hide a line, players can block others (unblock all from the chat screen), lines are kept 7 days. The newest world line runs over the bottom of the lane (tap it for CHAT). |
| Salt Exchange | Owner, 24 Sep 2026: "a global trading screen that all players can list their items for gold or buying from them". Built as the GDD's Salt Exchange for gear: any bag piece listed for sorn; buyers pay the price, the seller gets it less the GDD's 5% tax. Assumptions: 1,000 to 1,000,000,000 sorn, 10 listings a player, 48 hours then the piece goes back; worn pieces must be taken off first; a listed piece cannot be worn, forged or turned; BUY pages by slot and order (cheapest, newest, highest +). Materials, shards and consumables are not tradable yet. |
| Accounts | Owner, 24 Sep 2026: "a sign up sign in screen". Email and password (PBKDF2-SHA256, 210,000 iterations); CREATE ACCOUNT saves them to the hero being played; SIGN IN points this phone at an account (a guest hero with progress is warned first); SIGN OUT starts a new guest; sessions are per device, so one hero can be played on two phones. Shown once after the title screen on a guest's first launch (then from MENU, ACCOUNT). Assumptions: 8+ character passwords, 8 wrong tries per email per 15 minutes; no email verification or password reset yet (no mail sending). |
| Sign in with Apple / Google | Owner, 24 Sep 2026: "i want to add sign in through google and apple". Built as a browser sign-in for both, on both platforms: the game asks the server for the provider's page, opens it in Apple's in-app sign-in sheet (iOS, `Plugins/iOS/OrsuunAuth.mm`) or the browser (Android), the provider returns to the server, which checks the signed ID token against the provider's published keys and sends the app a one-time ticket on an `orsuun://auth` link; the ticket only works on the device that started. A login already linked to an account switches the phone to it; a new one is linked to the account being played. Google is live; Apple needs the owner's keys (see "Store release"): its button shows on the sign-in screen and says so when tapped. The Development server's stand-in provider ("dev", MENU > DEV: TEST SIGN-IN) walks the same round trip. A native iOS Apple button (no sheet) can come later: the server already takes ID tokens at `/v1/auth/external`. |
| Moderation | Owner, 24 Sep 2026 ("go" on the moderation tool before inviting testers). A web page at `/admin` (served by the game server) for moderators: game accounts whose email is in `Admin:Emails` (`ADMIN_EMAILS` in the server's `deploy/.env`; the owner's is set). Sign in with that account's email and password (12-hour session). Tabs: overview, the report queue (hide, keep, all lines of a player), world chat search, players (mute 1 h / 24 h / 7 days, ban with a reason the player sees, unban), guilds (rename, disband), and the moderation log. A ban blocks sign-in, closes their Exchange listings, takes them out of their guild and hides their lines. |
| Bounties and Hunt Marks | Owner, 24 Sep 2026 ("do all of them"; the GDD's Hunt Marks). Five daily and four weekly bounties counted by the server (Korstones, hunting minutes, forges, turns, Commander fights, pushes, sieges), reset at 20:00 server time (weekly on Mondays); the Hunt Marks shop sells Etching Needles, Pinning Wax, Turnstones, Scrolls of Mercy and Draughts. ETCH (Etching Needle, 1st to 4th etching at 100/80/60/40%) and PIN (Pinning Wax, one lock per item, turns cost two, unpinning spends the wax) are on the Forge. The owner will add monetization; the shop prices are placeholders. |
| Guild war and fortress bids | Owner, 24 Sep 2026 (desktop app session): picked all four offered next steps, among them "guild war and fortress bids". Built asynchronous like the sieges (the GDD's live 20v20 and 50v50 are out of reach for now). Guild war: the leader signs up (3+ members), war nights Wednesday and Saturday 21:00 pair guilds by Elo rating for an hour; each member fights up to 6 duels, 2 min apart, on one of three lanes against a drawn member of the other guild; a win is a kill and pushes the lane (two steps under the war flag the leader or an officer plants), 5 steps break it; score = kills + 10 per broken lane; winner 150,000 treasury sorn + 100 guild XP (draw 50,000 + 60, loss 30 XP); a duel pays 5,000 sorn + 1 Hunt Mark (GDD: PvP pays currency, never upgrade protection). Duels follow the GDD's PvP balance: gear and level on a class-neutral frame, stats above the pair's median compressed by 30%, a seeded roll tuned so a +9 set beats a +7 set about 80% of the time (`Rules/GuildWar.cs`: `GuildWars`, `Duels`). Fortress keeps: the Banner sieges stay; the keep decides the guild flag. Leaders or officers bid treasury sorn on one keep a week (50,000+); Sunday 20:00 the top four bids contend (spent, the rest refunded) and storm the keep for an hour while its holders mend it; the best contender takes it past 150,000 + the mending. The holder flies its flag (+2% sorn) and earns 2% of the Exchange tax. All numbers are assumptions (not stated by the owner). |
| The Caravan, Amber and the wardrobe | Owner, 25 Sep 2026: "add skins, mounts and companions that have expire time, like 1-3-5-7-14 days. equipabble and changeable at gear or a different screen. higher level bosses can drop these, and also we will add a new currency that is only buyable with real money"; a shop screen shown first as a Higgsfield mockup (`docs/concept/screens/mockup-caravan-*.jpg`, `mockup-wardrobe.jpg`). Answers: the currency is **Amber** (not the GDD's Aurels; real money only); **small stats, Metin2 style** (skin HP, mount attack, companion hunting XP or sorn); **mounted combat** (the hero rides and fights from the saddle); **the shop sells every duration, bosses drop short ones** (1-3 days, rarely 5-7), a piece held again adds its days. |
| Characters and the depot | Owner, 25 Sep 2026: "add character creation with name selection after signing up or loginning in like metin2 screen, total 4 char slots with a common depot of items to trade between each other". Answers: Amber is shared by the account's characters (everything else per character: sorn, level, gear, wardrobe, guild, Pits); the Banner is chosen per account (all four fight for it). |
| The way in | Owner, 25 Sep 2026: "when we first download the game, the game should start at login sign up screen with google apple etc, after that the game should ask for banner when we create the account after that we need to go to character selection screen that we already have with max 4 characters to create a character", and "for a sec the game screen comes before signin login character selection etc". Built: a new install (or a phone after SIGN OUT) shows the title only while it connects, then the sign-in screen (Apple, Google, create account, sign in, play as guest); an account not yet sworn swears its Banner next (at the character screen, `POST /v1/lobby/banner`, every hero of the account rides under it); then the character screen. The game never shows before a hero is chosen: the character screen covers it ("Connecting...") from the moment the title fades. CONTINUE WITH APPLE is shown although Apple sign-in needs the owner's paid Apple Developer account: until `APPLE_SERVICES_ID` is set, tapping it says so. PLAY AS GUEST stays (assumption: store review prefers a way in without an account). |
| Friends, chat actions, guild invites | Owner, 25 Sep 2026 (asked for next, with maps 9 and 10, Banner change and Oath Renewal, guild invites and Pit seasons): "also add sending trade invite fuild invite from chat also add adding friends and friend list as well". Built: tap a name in CHAT for ADD FRIEND, TRADE, INVITE TO MY GUILD (leader or officer), REPORT, BLOCK; a FRIENDS screen (MENU, or the button in CHAT). Assumptions (not stated by the owner): friends are each hero's own (like guilds and trade), mutual (a request the other hero takes; asking one who asked you makes you friends at once), at most 50 friends and 20 requests waiting; a hero is online while its heartbeat is under 2 minutes old; blocking ends a friendship and stops requests and invites from the blocked side. Guild invites: the leader or an officer invites by name (guild screen, ASKING / INVITE) or from chat and the friend list; an invite lets the hero in even through shut gates, lapses after 3 days, a guild holds 20 waiting. The HUD calls once for new friend requests or a guild invite. Private messages came later (their own row). |
| Maps 9 and 10 | Owner, 25 Sep 2026 (with the friends list, Banner change and Oath Renewal, guild invites and Pit seasons). Built from the world bible: Colossus Graves (levels 74-82: stone giants, bone pickers, siege beasts; Giant's Knuckle; Hurm the Unburied) and the Sunken Bazaar (82-90: Khan cultists, gilded constructs, debt wraiths; Gilded Cog; the Last Merchant-Prince), campaign to 100 stages; floors picked by the owner (Graves A, Bazaar C after "try again" on the first two). Assumptions: both bosses 105% like the Coil Mother (every class clears them with an Epic +9 set of the map's level, 15-20 of 20; with the Bloodbirch's Epic +9, 0 of 20); looks for item levels 80-99 drawn with them, and bands 8 and 9 renamed after their art (Colossus Glaive / Gravewrought Lamellar; Khan's Crescent / Khan's Lamellar moved up a band; the Korstone names are gone). Zones now start at id 101 (campaign stage 100 was the old first zone id); maps 11 and 12 need the zones moved first. |
| Banner change and Oath Renewal | Owner, 25 Sep 2026 (picked from the offered list: "change your account's Banner once a season (the world bible's defection), with Oathstones from the Carvers' Archive as the cost"). Built: the War screen's CHANGE opens the oath screen in a change mode; the account's Banner changes for every hero, once a War season (a week in the playtest), for 5 Oathstones paid by the hero who swears anew; points already won stay with their Banner; world chat announces it. Oathstones exist now: the Last Carver's chest always holds one, two with the vault open. Oath Renewal from GDD section 12: at level 105 a hero renews (tap the hero line on the Gear screen), back to level 1 with +3% attack and HP for good, up to 10 times; its time is settled and the lane reseeded as with a change of class. Assumptions (not stated by the owner): the price (5), that renewal costs nothing but the levels, that duels and the Pits leave the renewal bonus out (like the wardrobe), that Oathstones are not yet used for Grand skill grades (skill grades are not built). |
| Pit seasons | Owner, 25 Sep 2026 (picked "guild invites and Pit seasons": "the Pits get weekly seasons with ranks, rewards and the Pit shop"; GDD: the Pit ladder resets weekly and pays titles and season currency; Pit rewards are currency and cosmetics, never upgrade protection). Built: a season is the War season (the bounty week); the board shows the season (its record, not the lifetime one); when the week turns WorldClock settles the season once: every hero with 3 or more fights that season gets Laurels by the league they ended in (Bronze 20, Iron 40, Silver 70, Gold 110, Jade 160, Khagan 220), the first 100 more and the title Champion of the Pits, the second and third 50 more and Pit Veteran (titles shown on the board through the next season), world chat names them, and every rating moves halfway back to 1,000. The Pit shop adds 5 Turnstones (15), an Etching Needle (20), Pinning Wax (25) and an Oathstone (45) to the Korshards. Assumptions (not stated by the owner): all the numbers above; Technique Scrolls and frames still wait for skill grades and name frames. |
| Skill grades | Owner, 26 Sep 2026 (the first of the four queued steps), and while it was built: "make all books seperate, each classes each skill need a seperate book, and for m1 to m2 1 book m2 to m3 2 book and m3 to m4 3 book etc. but for g always one for each level, make it %60 succes but we need a good hardening thing there"; asked, the owner picked Honor as the hardening, 70% a read (the GDD's), and books that drop for any class and trade. Built (`Rules/SkillGrades.cs`): twelve Technique Scrolls, one per skill of each class (book id = class * 3 + slot); a skill climbs Normal, M1-M10, G1-G10, P. A Mastered step reads the skill's own scroll: one scroll a read, 70%, the skill rests 8 hours after each read, and the step needs 1, 1, 2, 3 .. 9 good reads (46 in all). A Grand step or Peerless burns one Oathstone at 60% and pays Honor, 30 times the Grand step tried (G1 30 .. G10 300, Peerless 330). Honor: 1 a Korstone, 5 a Pit win, 10 a dungeon Warden. Power: +2% skill power a Mastered step, +3% a Grand step, +10% for Peerless (+60% in all); a haste skill's duration grows by half as much. Scrolls come from Warden chests (one at random), the Hunt Marks shop (10 marks, one of your class's three at random), the Pit shop (30 Laurels), the Salt Exchange and direct trade (stacks). Assumptions (not stated by the owner): all the numbers except 70%, 60% and the read counts; grades belong to each class's skills, so a class switch fights with that class's grades; the GDD's Normal 1-17 is folded into the hero's level; the Scroll of Clear Mind and Scholar's Incense are not built. |
| Inventory screen | Owner, 26 Sep 2026: "we need a good inventory screen then as well, make it with higgsfield and show me first", then "make 10 designs total", and picked D (`docs/concept/screens/mockup-inventory-D.jpg`; A-J beside it). Built as the Gear screen, now titled INVENTORY (the hunt's tile says INVENTORY): the hero turning on the painted steppe between the eight worn pieces, level, attack, defense, HP and crit beside him, SKILLS and the class switch under him; ALL / GEAR / BOOKS / MATERIALS over one grid; sorn, Amber and Honor along the bottom; WARDROBE, DEPOT, BACK. A tile opens its card: a piece (EQUIP, FORGE / TURN, SELL on the Exchange), a scroll (READ opens SKILLS for its skill, SELL), a material (what it is for). New icons made on Higgsfield in the icon set's style: four class scrolls, Master's Needle, Hunt Marks, Laurels, Guild Tallies, Summoning Marker, Honor. |
| Five skills a class, effects and animations | Owner, 26 Sep 2026: "we need to have total 5 skills at each hero maybe other ones can unlocked at higher levels. also we need good and different effects and animations for each of the 20 skills. work on them and show me results before implementing them", then "also make skills effect better for master and grand levels and of course coolest for perfect"; the Skill Codex (https://claude.ai/artifact/RR175CzDeGFtnuUTkt4X2h) was approved: "the decisions are good, build them". Built: the world bible's fourth and fifth skills of each branch, at levels 30 and 60: Vanguard Honed Edge (attack +25% for 8 s) and Bull Rush (180% to every enemy, a 2 s stun), Kestrel Venom Cloud (45% a second to every enemy for 6 s, never crits) and Shadow Stoop (500% on the mark, double below 30% health), Wraithsworn Grave Chains (6 s: enemies strike half as often, +30% damage taken) and Shroud of Night (a veil taking 25% of max HP for 8 s), Drumcaller Hunter's Blessing (+20% crit for 8 s) and Mirror Ward (8 s: blows 20% lighter, 40% returned). Map bosses and Commanders shrug off the stun and the slow. Twenty Technique Scrolls. A cast animation per skill and an effect per skill that grows with the grade (Mastered: echo and ground glow; Grand: gold, the rune circle, a follow-up and shafts of light; Peerless: the class spirit, the hero's own model larger in ghost light, a white-gold core, cracks, the crown and a shake). Assumptions (not stated by the owner): all the numbers; bosses immune to stun and slow; map bosses whose map ends at level 30 or more have 12% more health, 60 or more 25% (the new skills' damage, so the gear checks hold). |
| Mounts and skills | Owner, 26 Sep 2026: "at mount make char only autoattacking, off the mount it can use skills." Built: a hero wearing a mount makes plain attacks only (no skill, manual or AUTO; the offline hunt counts no skills either); the hero plate has DISMOUNT / MOUNT UP (the last mount ridden, or the held one with the most time). |
| Lane camera | Owner, 26 Sep 2026: "we can put the char and the mobs a bit lower as well". The lane camera sits 0.8 m higher (heads, bosses and skill effects clear the goal plate and banners); the backdrop moved with it. |
| Item images by level | Owner, 26 Sep 2026: "we need different images for all levels different items". Built: an icon for every item at every level band (154, `Resources/Icons/Items`): the weapon and armour as the playing hero's class wears them (the Vanguard's glaives and armours, the Kestrel's knives and outfits, the Wraithsworn's sabres, the Drumcaller's staves), the helmet, shield and boots as the Vanguard's look of that band wears them, and a bracelet, necklace and earrings in each band's materials; made on Higgsfield from renders of the models, in the old icons' style. Shown wherever a piece is (inventory, forge, smith, turning, sockets, trade and Exchange cards). |
| Average damage and skill damage | Owner, 26 Sep 2026: "after level 30 we need to add 'ortalama zarar +%...' like metin 2 to all weapons, it will be nearly impossible to have +%60, it will be from -%30 to +%60. and also we will have same for 'skill damage +%30' to be impossible from -15 to 30". Built (`Rules/Items.cs` `WeaponRolls`): every weapon of item level 30 and up rolls both when it drops and keeps them; average damage changes plain attacks, skill damage every skill's damage (poison included), and the offline hunt counts both. Assumptions (not stated by the owner): the curve (average damage centres on +10%: a quarter of weapons roll below zero, 6% reach +30%, one in a thousand +50%, one in about 60,000 +60%; skill damage the same at half the scale, one in 30,000 +30%); the rolls never change (no forge or turning touches them); weapons that existed rolled once in migration `WeaponDamageRolls`; Pit shades keep their weapon's rolls. Cards show them in gold near the top and red below zero. |
| Upgrade glow like Metin2 | Owner, 26 Sep 2026: "our +7,8,9 effects are so bad, make it more like metin2 like glitter and shit". Built: the flat orange wash is gone; the piece keeps its own look, polished: a band of sheen slides over it, the rim glows in the level's colour, glitter twinkles across the surface (`EmberGlow.shader`), and star sparkles rise off it (`GearSparkle`), more each level; the inventory's hero now shows its glow too. The level colours stay the owner's (pale gold +7, gold +8, ember-gold +9). |
| Vanguard redesign | Owner, 26 Sep 2026, with Metin2 warrior pictures: "i want vanguard and its armors more to be like this", then (after two rounds of concepts, `docs/concept/vanguard-redesign/`) "closer to the pictures yes but no need to be darker", "Yes, build it", and for weapons "it can change, glaive sword one handed two handed" / "Mixed by level". Built: the Vanguard is a broad heroic martial-arts warrior (a red headband, his hair tied in a topknot so he reads as no other game's hero, great flared pauldrons, a long front tabard, baggy trousers into armoured boots, no shield), eleven armour bands brightening from a quilted vest and red lamellar through bronze, wolf-fur steel, silver, bronze-and-teal, ember-red, blued steel, bone-white and white-and-blue to gold and crimson (the armour names are kept); weapons take turns by band: Herder's Sword, Rider's Glaive, Horsebreaker Greatsword, Tamga Sword, Crescent Glaive, Banner Greatsword, Emberwake Sword, Oathkeeper Glaive, Colossus Greatsword, Khan's Crescent (a sabre), Glaive of the Nine Oaths (`ItemLooks.WeaponKinds`). Swords rise from his fist, glaives stand from the ground past his head. Assumptions (not stated by the owner): the band designs and the new weapon names; wardrobe skins keep their older costumes. Also from the owner's picture of a Metin2 +9 weapon ("you can check the shinings I want from here"): a glowing weapon wears a fiery halo, faint at +7 and blazing at +9. |
| Maps 11 and 12 | Owner, 26 Sep 2026: "finish maps then" (the queued step after private messages). Built from the world bible: the Thousand Markers (levels 90-98: the Hollow Khan's buried army risen, troopers, riders and captains; Marker Dust; Varkesh of the Left Wing, the bible's "General Varkesh of the Left Wing") and the Hollow Throne (98-105: the Guard of the Khan, as throne guards, the Khan's hounds and oath chanters; Throne Shard; the Khan's Shadow, the Khan himself saved for a later season), campaign to 120 stages; weapon and armour looks for item levels 100-105 (band 10, every class). Floors: the owner turned down the first six ("they all look unrelated to background"); the second six were made from each backdrop's own foreground ground and colour-matched to it, and the owner picked the Markers' trodden stone road and the Throne's worn flagstones (C and C). Assumptions (not stated by the owner): the mob and boss designs (violet oath-light for the risen army, molten gold for the Khan's court), both bosses 105% like the last three; Varkesh falls to every class with an Epic +9 set of level 98 (15-19 of 20), the Khan's Shadow, whom heroes meet at the level cap, to an Epic +9 set at 105 about half the time (10-15 of 20) and a Legendary +9 set nearly always (18-20). |
| Private messages | Owner, 26 Sep 2026: "go for private messages" (the queued step), then "they need to stay after days and days" and "make it a screen". Built: a MESSAGES screen (MENU, or MESSAGE on a name in CHAT or the friend list, or the HUD's call): the conversations newest first (online dot, class and level, the last line, how many are new), a name box to write to any hero, and a conversation view (their lines left, yours right in gold, times, LOAD OLDER, updates every 3 s while open). Messages are kept with no expiry. The HUD calls "A NEW MESSAGE: READ IT" (after guild invites and friend requests). Assumptions (not stated by the owner): any hero can write to any hero by name, not only friends; the chat's rules hold (the word filter, 200 letters, the flood limit shared with chat, moderator mutes); a block works both ways (neither side can write) and shows in the conversation; your own login's heroes cannot write to each other; a very long conversation keeps its newest 1,000 lines; tapping a line they sent offers REPORT, which puts a copy in the moderation page's chat queue (channel "message"); deleting a hero deletes its messages both ways. |
| Campaign Trail | Owner, 25 Sep 2026: asked "what to do next", picked "Campaign Trail (Recommended), Maps 5 and 6, Player-to-player trade, More dungeons" (built in that order). From the GDD: an 8 week season of 50 tiers; the day's missions (bounties) pay Trail XP; the free track pays Turnstones, Scrolls of Mercy and a Khan's Alloy every 10 tiers; the paid track the season costume, a mount, 300 Turnstones, 20 Khan's Alloys and 3 Anvil Wards; $9.99, "premium plus" $19.99. Assumptions (not stated by the owner): the paid track costs 650 Amber (the $9.99 pack's) and Trail Plus 1,400 (the $19.99 pack's) with 10 tiers at once, 750 from the Trail; each character climbs and buys its own Trail (only Amber is shared); 600 XP a tier, 100 per daily bounty and 500 per weekly (every daily and one weekly a week finish it in the 8 weeks, the dailies alone do not); free track 5 Turnstones on odd tiers and a Scroll of Mercy on the other even ones; the season's pieces are held until the season ends (at least 14 days) rather than the Caravan's 1-14 days; rewards left unclaimed are handed over when the next season starts; season 1 is "The Amber Road", Mon 21 Sep to Mon 16 Nov 2026 20:00, with the Amber Road Regalia (skin, +5% HP) at paid tier 1 and the Amber Road Courser (mount, +5% attack) at paid tier 50. The Trail does not add to offline yield yet (the GDD lists it among the B_afk sources). |
| Direct trade | Owner, 25 Sep 2026: the third pick ("Player-to-player trade"), and while it was built: "at trade we need to see stats of items, maybe with clicking" (tapping a piece opens its stat card). From the GDD (section 8, "Direct trade window"): a two-step confirm with a 5 second lock after any change to the offer; level 30 and a 72 hour old account; a 2% tax on the sorn. Assumptions (not stated by the owner): up to 8 pieces and any sorn each side; both lock, then both confirm; an invitation lasts 3 minutes and an idle window 10; your own heroes cannot trade with each other (they share the depot); on the Development playtest server the level and age rules are off so it can be tried at once; the GDD's 12 hour hold for trades far off the Exchange median is not built (no price history yet). |
| More dungeons | Owner, 25 Sep 2026: the fourth pick. From the world bible: Silkmother's Warren (under the Salt Sea, 2 levels, the Silkmother, drops Khan's Alloy) and the Carvers' Archive (a Sky Banner vault, puzzle-light, the main source of Master's Needles and Oathstones). Assumptions (not stated by the owner): the Warren opens after the Salt Sea (stage 30), 6 floors in two levels (the Upper Galleries, the Brood Deep, opened by an egg-nest rush), the Silkmother's chest always holds a Khan's Alloy; the Archive opens after Whitefang (stage 40), 5 floors, the puzzle is a rune lock on floor 3 (an original steppe riddle carved in the vault door, three runes to choose from), the right rune makes the Last Carver's chest hold a Master's Needle (10% otherwise); the Master's Needle now exists as an item and adds the fifth etching (it could not be added before); Oathstones wait for Oath Renewal (not built); the two free keys a day are shared by all three dungeons; every dungeon's top floor is 40% above its first (the Spire's 5% a floor is unchanged). |
| Lane floors | Owner, 25 Sep 2026: "no floor on the maps right? ... create some floors related to background and show me them together, and we will pick" (Higgsfield Ultra bought for it), then picked one by one from real lane renders: the Oathfields / Ember Steppe F (an old flat-stone road under the hero, after rejecting A and B), Korstone Fields A (cracked orange earth), Gorak Pass / war camp A (muddy firelit camp earth), the Salt Sea A (hexagonal salt crust), Whitefang B (a packed snow trail), the Hollow Spire A (grave-plain earth), the Cinder Marches A (basalt with lava cracks), Whisperwood B (a mossy stone path), Silkmother's Warren B (silk-covered salt flagstones), the Carvers' Archive A (carved tiles with glowing runes). |
| Maps 7 and 8, looks 60-79 | Owner, 25 Sep 2026 ("what to do now", picked "Maps 7 and 8"): the world bible's Bloodbirch (red forest, levels 58-66: red treants, birch stalkers, sap horrors; Bloodbirch Resin; the Rootfather) and Drowned Steppe (swamp, levels 66-74: marsh serpents, bog riders, leech swarms; Serpent Scale; the Coil Mother), with the item looks for levels 60-79 (bands 6 and 7: the Emberwake Glaive / Emberplate and the Oathkeeper Glaive / Oathsworn Harness, and each class's own) since the new maps drop them. Floors picked by the owner: the Bloodbirch B (a stone path under red leaves), the Drowned Steppe B (a plank boardwalk). Assumptions: past the +9 cap the climb is item level and rarity (the Rootfather wants a Rare +9 or Epic +8 set at level 66, the Coil Mother a Rare +9 or Epic +9 at 74; the Wraithsworn wants Epic there); the Coil Mother has +5% HP and attack so the Drowned Steppe's gear stays a gate (`MapDef.BossPercent`). |
| Server authority | Every roll, reward and trade is decided by the server. The client sends intents and replays seeds. |
| Storage | PostgreSQL from day one (dev runs it locally). |
| Map roles | Hunting Grounds (sorn, levels), Korstone Fields (materials, Turnstones, Korshards), Commander Grounds (bosses, skins). Campaign stages are the unlock spine. GDD section 13. |
| Boss brackets | Simulated rivals until the multiplayer milestone; real shared HP pools later. |
| Playtest | Phase 0 grey-box playtest done, owner reported it fine. First on-device playtest against the live server on the owner's iPhone, 23 Sep 2026: owner reported it good. |
| Art direction | B, modernized classic (GDD section 14), picked by the owner on 23 Sep 2026. D (ink and ember) was the runner-up. |
| Upgrade glow and looks | Owner, 23 Sep 2026: only the weapon and the body armour are visible on the character and glow, each by its own level from +7 up, in classic MMO upgrade shine (aura, flowing light, sweep; pale gold +7, gold +8, ember-gold +9). Helmet, shield, bracelet, necklace, earrings and shoes are stats only: forged like the weapon, no look, no glow. |
| Item looks | Owner, 23 Sep 2026: weapon and body armour looks change every 10 item levels (`ItemLooks`: 11 bands, Vanguard names from Herder's Glaive / Quilted Coat up to Glaive / Harness of the Nine Oaths). Art exists for every band since 26 Sep 2026 (band 10, levels 100-105, came with maps 11 and 12). No armour equipped shows the band-0 Quilted Coat. |
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

## Where we left off (25 Sep 2026, desktop app session)

The owner moved to the Claude desktop app (Code tab, `~/orsuun`). On 24 Sep they picked four next steps, all built,
pushed and installed: the first ten minutes (spotlight tutorial, next-goal line), the rest of the mockup look, the
Mirage Queen (she already had a real rigged model; only the notes were stale), and guild war with fortress bids (see
the decision row and "Done ... desktop app session"). Build 26092419 is on the owner's iPhone (24 Sep 2026, 22:34); the
playtest server is deployed (22:50, migration `GuildWarKeeps` applied) with a home page at `/`
(`deploy/site/index.html`, routed in the Caddyfile; a single-file bind mount keeps the old Caddyfile after `git pull`,
so restart the caddy container after changing it). A copy of the playtest database from just before that deploy is on
the Mac: `~/orsuun-backups/playtest-before-guildwar-2026-09-24.sql.gz`. Google sign-in is published (owner, 24 Sep
2026: "published"); its Branding page wants the home page `https://65.108.221.210.sslip.io/`. The Android APK of
that build is on the download link (24 Sep 2026, 22:56); the previous one is kept beside it as
`/opt/orsuun/downloads/Orsuun-prev.apk`.

On 25 Sep the owner picked more campaign maps, dungeons and the Pits; all three, and a class rebalance, were pushed,
deployed (migrations `Dungeons` and `Pits`; database copy first: `~/orsuun-backups/playtest-before-dungeons-pits-2026-09-25.sql.gz`),
installed on the iPhone and put on the APK link (25 Sep 2026, about 01:45). Then the owner asked for timed skins,
mounts and companions, Amber and a shop screen (the Caravan and the wardrobe: deployed with migration `Wardrobe` and on
the APK, 25 Sep; the iPhone was not connected), costume models for the skins (APK), and Metin2-style character select
with four slots and a shared depot: pushed and deployed on 25 Sep (migration `Characters`, which reshaped every hero
into slot 1 of its own login; copy first: `~/orsuun-backups/playtest-before-characters-2026-09-25.sql.gz`), APK on the
download link. Installed on the owner's iPhone on 25 Sep 2026 (after it was plugged in).
Then the owner asked "what to do next" and picked all four offered steps, built in order the same day and each pushed and
deployed with a database copy first (`~/orsuun-backups/playtest-before-{trail,trade,dungeons2}-2026-09-25.sql.gz`): the
Campaign Trail (migration `CampaignTrail`), maps 5 and 6 (the Cinder Marches, Whisperwood; no migration), direct
trade with a stat card for every piece (the owner asked for it while it was built; migrations `DirectTrade`,
`TradePieces`), and two more dungeons with the Master's Needle (migration `MoreDungeons`). See the four "Done 25 Sep
2026" sections below. fal.ai ran out of balance on 25 Sep 2026 (evening; "User is locked. Reason: Exhausted balance", the
owner must top it up at fal.ai/dashboard/billing); art since then comes from Higgsfield (the owner's Ultra plan, about
1,700 credits left): gpt_image_2_5 sheets and Tripo H3.1 multiview (`tripo_h3_1_multiview_to_3d`, 9 credits a model),
upscales done locally. Before that:
`tools/art/fal.py`. The APK with all four is on the download link and installed on the owner's iPhone (25 Sep 2026).
Then maps 7 and 8 (with looks for levels 60-79) and the way in (sign-in screen first, then the Banner, then the
character screen; deployed, no migration) were built, pushed, deployed and put on the APK link and the iPhone.
The owner's first playtest hero (a guest from 24 Sep, Ember Banner, a guild leader) was deleted from a device with MENU ->
DELETE ACCOUNT on 24 Sep; the owner does not want it back (25 Sep 2026: "not necessary"). Do not ask again.

Waiting on the owner: the Hetzner Storage Box for database backups (they will buy it later); the paid Apple Developer
Program (TestFlight, Sign in with Apple, no 7-day expiry); the monetization plan; a real domain before release.

The maps 9 and 10 build and then Banner change and Oath Renewal went to the APK link and the owner's iPhone (25 Sep 2026,
evening); Pit seasons went to the APK link. Skill grades and the inventory screen (26 Sep 2026, about 01:30) are deployed
(migration `SkillBooksHonor`; database copy first: `~/orsuun-backups/playtest-before-skillgrades-2026-09-26.sql.gz`),
on the APK link and installed on the owner's iPhone (build 26092522, which also carries Pit seasons). Five skills a
class with their effects, mounts without skills and the lower lane (26 Sep 2026, about 03:40) are deployed (migration
`FiveSkills`; copy first: `~/orsuun-backups/playtest-before-fiveskills-2026-09-26.sql.gz`), on the APK link and on the
iPhone (build 26092600). Private messages (26 Sep 2026, about 04:40) are deployed (migration `PrivateMessages`;
copy first: `~/orsuun-backups/playtest-before-messages-2026-09-26.sql.gz`; `smoke-whispers.sh` passed live and cleaned
up), on the APK link and on the iPhone (build 26092601). Maps 11 and 12 with the looks for levels 100-105 (26 Sep
2026, about 06:30) are deployed (migration `MoveZones`; copy first:
`~/orsuun-backups/playtest-before-maps11-2026-09-26.sql.gz`; the one hero parked in a zone moved from 111 to 211), on
the APK link and on the iPhone (build 26092608). The APK is now 399 MB.
The GDD has notes for all of this session's features (25 Sep 2026, evening).
**Queued by the owner (26 Sep 2026: "all of these sound good ... I will ask u something than do all of these", then
"go handoff do these 4 pls"):** skill grades (done 26 Sep 2026 with the inventory screen the owner asked for while it
was built; see the two decision rows), then maps 11 and 12 (the Thousand Markers, the Hollow Throne; done 26 Sep 2026, see its decision row), private messages (whispers; done 26 Sep 2026, see its decision row), a smaller download (the APK is 399 MB since maps 11 and 12; Google Play's limit is 200 MB), in that
order. The owner will not top up fal.ai: art runs on Higgsfield (see CLAUDE.md).
**Asked while maps 11 and 12 were built (owner, 26 Sep 2026), to do next in this order:** "we need different images for
all levels different items" (an icon per item for every level band, not one per slot); "after level 30 we need to add
'ortalama zarar +%...' like metin 2 to all weapons, it will be nearly impossible to have +%60, it will be from -%30 to
+%60. and also we will have same for 'skill damage +%30' to be impossible from -15 to 30" (average damage and skill
damage rolled on weapons of item level 30 and up, both skewed so the top is nearly impossible); "our +7,8,9 effects are
so bad, make it more like metin2 like glitter" (the upgrade glow of the weapon and armour).
**Asked next, ahead of maps 11 and 12 (owner, 26 Sep 2026; built and shipped the same night, see the decision rows):** "we need to have total 5 skills at each hero maybe other ones
can unlocked at higher levels. also we need good and different effects and animations for each of the 20 skills. work on
them and show me results before implementing them", then "also make skills effect better for master and grand levels and
of course coolest for perfect". Proposal published for review (nothing built yet):
https://claude.ai/artifact/RR175CzDeGFtnuUTkt4X2h (the Skill Codex: the eight new skills from the world bible's branch
lists, Honed Edge and Bull Rush, Venom Cloud and Shadow Stoop, Grave Chains and Shroud of Night, Hunter's Blessing and
Mirror Ward; skill 4 at level 30, skill 5 at level 60; a Higgsfield picture of every skill at Normal, Mastered, Grand and
Peerless and a Kling clip of each; the Peerless class spirits). Wait for the owner's picks, then build: SkillDef kinds
and numbers through the simulator, `SkillGrades.Slots` 5 and 20 scrolls (grades and `BookStacks`/listings renumbered
from class * 3 + slot to class * 5 + slot in a migration), five HUD buttons with locks, a cast clip per skill in
Blender, and the effects per tier in `LaneView`.
Could come next: Oath Renewal (Oathstones for the Archive),
the Exchange's price history (the direct trade 10:1 hold needs it), Campaign Trail season 2 art before 16 Nov 2026, name frames in the Pit shop, the Mirage Queen's presence (a mirage shimmer,
ghostlier images, a little taller), Free Lances and the fortress aura for keeps, password reset by email
(needs a mail service).

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

## Done 25 Sep 2026 (campaign maps, dungeons, the Pits)

- Owner, 25 Sep 2026, asked what to develop next and picked more campaign maps, dungeons and the Pits.
- Campaign maps 2-4 from the world bible (section 6): Gorak Pass (levels 10-20, war hounds and Gorak marauders,
  Marauder Brand, boss Warlord Tul-Gorak), the Salt Sea (20-30, the Salt Flats' scorpions, snakes and ghouls, Scorpion
  Glass, the Mirage Queen), Whitefang Range (30-40, the Frost Pasture's bears, wights and hags, Frozen Marrow,
  Nine-Winters the ice wight lord). 40 campaign stages now. The Oathfields' curve is unchanged; past it HP climbs 19.7 and
  attack 13.8 points a stage, simulated so each map boss needs that map's gear (a level-20 +6 set clears Gorak Pass 10,
  a level-10 +6 set never does; level 30 +7 for the Salt Sea, level 40 +7 for Whitefang) and a new map opens just above
  the last one's mobs (`StageAndGearTests`). Zone unlocks are unchanged (all zones still open by stage 10), so the new
  maps are the push spine and higher campaign farm spots. Client: Gorak Pass uses the war camp backdrop with the wolf
  and deserter recoloured (`LaneView.GorakMobs`, "Model#RRGGBB" tints a set entry), the Salt Sea and Whitefang Range the
  Salt Flats' and Frost Pasture's sets and backdrops, Nine-Winters is a tall pale ice wight; Zones shows each map's
  picture; the goal chain names each map boss. `-stage <n>` parks local play at a stage for screenshots.
- Dungeons: the Hollow Spire (`Rules/Dungeons.cs`, `DungeonTests`; world bible section 6). Nine floors pushed up from the
  grave plain: floor 3 a Korstone rush (an Elder at once), floor 6 the Chained Smith (one forge of a worn piece at +10
  points, `ForgeMethod.ChainedSmith`: the Forge's cost and failure rule), floor 9 the Spire Warden and his chest (Turnstones,
  an Etching Needle, a Korshard of the level's rank, 3 Hunt Marks, 10% a Khan's Alloy). Opens after stage 10; two free
  runs a bounty day (GDD: two keys a day); floors follow the highest cleared stage (+5% a floor, the Warden 115% of the
  stage boss), drop up to Legendary (GDD) and take about 5 minutes a run (one pack and a 35% Korstone a floor). Simulated:
  a hero geared for their stage clears it, one five levels and a forge level behind falls from level 20 on. Server:
  `GameService.Dungeons.cs` (enter fights floors 1-5 and stops at the smith, `Account.DungeonRunAtSmith`; the smith's
  answer fights 7-9 and pays the chest), migration `Dungeons`, endpoints `/v1/dungeon/enter|smith`, dev
  `/v1/dev/stage?cleared=`; `tools/smoke-dungeon.sh`. Client: the Spire's card leads the Zones list (ENTER, CONTINUE at
  the smith), floors replay in order on a new backdrop (`Resources/Backdrops/HollowSpire.jpg`, GPT Image 2.5 on
  Higgsfield, `docs/concept/env-hollowspire.jpg`) with the hollowed dead (greyed ghouls, deserters, wolves), the Warden
  a huge violet ice wight; `SmithPanel` asks at floor 6. Silkmother's Warren (needs spider art) and the Carvers' Archive
  (the Master's Needle source) are not built. Screenshot switches: `-dungeon` (online), `-smith`.
- The Pits (`Rules/Pits.cs`, `PitsTests`; GDD section 7): a 1v1 duel (`Rules.Duels`, the neutral frame, no
  compression) against another player's worn gear, with the attacker's edge (+0.1 on the duel's log-time scale, standing
  in for the GDD's manual casting). Five tickets a bounty day; three challengers at a time, players within 250 rating
  points who were about in the last two weeks, and where there are too few, Pit shades cut from the attacker's own gear
  one forge level weaker, even and stronger (an even shade is about 60%). Elo K 32 from 1000; a real defender moves half
  as far the other way (their snapshot fought without them), a shade not at all. Leagues Bronze, Iron 1100, Silver 1250,
  Gold 1400, Jade 1600, Khagan 1800. Laurels: 10 a win, 2 a loss; the Pit shop sells Trooper, Rider and Captain
  Korshards for 10, 25 and 60 (the GDD's Technique Scrolls and frames are not built). The board shows the top 20 with
  their weapons (the GDD's gear inspection, in brief). Server: `GameService.Pits.cs`, migration `Pits` (PitRating
  defaults to 1000), endpoints `GET /v1/pits`, `POST /v1/pits/fight|refresh|shop`, ledger kind `pit`;
  `tools/smoke-pits.sh` (run it locally: its fights move other accounts' ratings a little). Client: THE PITS button on
  the War screen, `PitsPanel` (record, challengers with odds, LOOK AGAIN, shop, board), the fight replays on the lane
  like a war duel with the rival dressed in their class and band (VICTORY / DEFEAT and the rating move). Screenshot
  switches: `-pits`, `-pitfight <n>` (online).
- Guild war now pays Guild Tallies (GDD currency table) to every member who fought: 10 for a win, 5 a draw, 3 a loss
  (`GuildWars.WinTallies`..., paid in `SettleWarAsync`, one UPDATE a side).
- The Caravan, Amber and the wardrobe (decision row above). Rules: `Rules/Wardrobe.cs` (`Wardrobe`, `Amber`;
  `WardrobeTests`). Fifteen pieces: skins Salt Nomad Garb, Frost Hunter Furs, Ember Khan Guise, Grave Warden Shroud
  (+2/+3/+4/+6% HP) and the Commanders' trophies Tul-Gorak's Warmask, Mirage Veil, Greyjaw Pelt Cloak (+4% HP, drops
  only, from their Commander's chest to damage ranks 1-5: 10% / 4%; they replaced the old permanent trophy names, which
  stay in `Inventory.Skins`); mounts Steppe Pony, Ember Warhorse, Gold Banner Charger, Hollow Steed (+2/+4/+5/+6%
  attack); companions Ember Fox (+3% hunting sorn), Sky Falcon (+5% hunting XP), Grave Wolf Pup (+6% sorn), Khagan's
  Eagle (+8% XP). Prices by tier for 1/3/5/7/14 days: 30/80/120/150/260, 60/150/220/280/480, 80/200/290/350/600,
  110/280/400/480/820 Amber. Drops: map bosses from level 20 (Gorak Pass's) 0.6% a kill (a hero parked on a map boss
  kills about 50 an hour), the Spire Warden's chest 10%; 1 day 60%, 3 days 30%, 5 days 7%, 7 days 3%. Amber packs
  60/300+30/650+80/1,400+200/3,800+600/8,000+1,500 for $0.99-$99.99, the first purchase pays its Amber twice. HP and
  attack go into `HeroFactory.FromEquipment(..., worn)` (server `Hero(account)`, client `PlayerSession.SetWorn`, so
  loop replays still match); the companion's XP or sorn is added by the server's `Apply(..., hunt: true)` on hunting
  gains (heartbeat settle, push, Commander fight, dungeon floors), after the replay, so it never changes a loop. Duels
  and the Pits measure gear only: the wardrobe does not reach PvP. Server: `GameService.Caravan.cs`, account fields
  (Amber, AmberPurchases, Wardrobe "id:expiresUnix;...", WornSkin/Mount/Companion), migration `Wardrobe`, endpoints
  `POST /v1/caravan/buy`, `/v1/caravan/amber`, `/v1/wardrobe/wear`, state `wardrobe` (pieces with seconds left);
  `tools/smoke-caravan.sh`. **Amber packs are granted free on a Development server (the playtest) and refused
  elsewhere with "store_closed"**: real purchases need the App Store / Google Play accounts, Unity IAP and server
  receipt checks (not built). Client: THE CARAVAN (`CaravanPanel`: tabs, a featured card, the five duration chips,
  BUY with a confirm, the Amber packs; opened from the camel button under the lane flag, which shows the Amber held) and
  WARDROBE (`WardrobePanel`: the three worn slots with time left, TAKE OFF, the held list with WEAR; from GEAR and the
  Caravan). Art: card paintings and pack pictures from GPT Image 2.5 (`docs/concept/caravan/`, in the game as
  `Resources/Thumbs/Caravan/<id>.jpg`), icons Amber, Caravan, WardrobeSkin/Mount/Companion, the Caravan scene; 3D
  from Tripo multiview sheets (`docs/concept/caravan/sheet-*.jpg`, cut into `<name>-front/side/back.jpg`, `art/blender/{warhorse,pony,fox,falcon}-tripo.glb`)
  through `looks.mob_model` (horses and fox rigged as quadrupeds; the falcon static): `Models/Mobs/MountWarhorse`,
  `MountPony`, `PetFox`, `PetFalcon`, and texture variants `MountWarhorseGold`, `MountWarhorseHollow` (cold glow),
  `PetEagle` (recoloured copies). On the lane (`LaneView.SetWardrobe`): the mount stands under the hero (saddle at
  56% of its height, the rider drawn 0.6 right of HeroX, mobs line up 1.3 further off), his legs held in a riding pose
  after the clips (`LateUpdate`, rest from the skin's bind poses); a ground companion trots at his feet, a bird glides
  ahead of him; the Grave Wolf Pup is the Hollow wolf at half size. Skins have their own costume model per class (owner, 25 Sep 2026: "dont forget skin thing"): 28 models, 7 skins
  x 4 classes, from turnaround sheets drawn with each class's own sheet as the reference (`docs/concept/skins/<class>-<Look>.jpg`,
  front/side/back crops in `crops/`, Tripo sources in `art/blender/skins/`). The Vanguard's go through
  `looks.armor_look(..., yaw_degrees=-90)` (the glaive is cut out, so the item's weapon look still shows) as
  `Models/Looks/Skin_<Look>`; the others through `looks.class_look` as `Models/Classes/<Class>_Skin<Look>` (Kestrel knives,
  Wraithsworn sword, Drumcaller staff). `LaneView.SkinModel` picks the costume when it exists; the old band-and-tint
  (`LaneView.SkinLooks`) stays as the fallback. Screenshot switches: `-caravan <tab>`
  (0 skins .. 3 Amber), `-wardrobe` (online).
- Characters (decision row above). Rules `Characters` (4 slots, a 40-piece depot, names of 3-16 letters or digits
  starting with a letter, clean by `WordFilter`, unique whatever the case; `CharacterTests`). Server: a `Login` table
  (the player's account: email and password, Amber and packs bought, the Banner, its devices and Google / Apple links
  via `ExternalLogin.LoginId`); each character is an `Account` row with `LoginId`, `Slot`, `Name`/`NameKey` (unique).
  Migration `Characters` turns every existing hero into slot 0 of its own login (same id) and moves its email, Amber
  and Banner there; names are filled once at startup (`GameService.BackfillNamesAsync`: the old generated name, which
  has a space, so it never collides with a chosen one). Every place that showed `Banners.GeneratedName(id)` shows the
  stored name. `Device.AccountId` is the character chosen on that device (Guid.Empty: the character screen). Lobby
  endpoints need only a session: `GET /v1/lobby`, `POST /v1/lobby/create|select|delete|register|signout|external/begin|external`;
  the game endpoints answer `no_character` until one is chosen. `/v1/auth/guest` takes `lobby: true` from this client
  (a new device then starts with no character); without it (older clients, smoke tests) a new device still gets a
  first character with a generated name, and email / Google sign-in pick the most recently played character, so the
  builds already out keep working. The oath sets the login's Banner and every character's (`SwearAsync`); new
  characters take it. Delete asks for the name typed; its depot pieces pass to another character of the login. MENU's
  DELETE ACCOUNT deletes the login with every character. Depot: `Item.DepotLoginId` (out of the bag like a listed
  piece: `Item.OutOfBag`), `GET /v1/depot`, `POST /v1/depot/put|take` (take is one guarded UPDATE, so two phones cannot
  both take a piece); the taker becomes the owner. `tools/smoke-characters.sh`. Client: the character screen
  (`CharacterPanel`, after the title and the account screen: the chosen hero stands large, rendered by `HeroStage`
  with its class, bands and skin costume, over four slot cards; START, DELETE with the name typed, CREATE with a class
  and a name), MENU's CHARACTERS goes back to it, GEAR's DEPOT opens `DepotPanel` (tap a row to move a piece).
  Screenshot switches: `-autoselect` (straight into the game with the last played character; every online shot needs
  it now), `-createhero`, `-depot`.
- Class balance past the Oathfields (owner, 25 Sep 2026: "balance"). Measured with full Rare sets at the stage's level,
  the Wraithsworn needed two to four more forge levels than the Vanguard at Gorak Pass, the Salt Sea and Whitefang, the
  Kestrel two more at Whitefang, and the Drumcaller two fewer. New class shapes (attack/defence/HP %): Kestrel 90/90/95
  (was 90/80/85), Wraithsworn 100/95/90 (was 105/60/80), Drumcaller 85/100/100 (was 95/110/105); crit, swing speed and
  weak points unchanged. Every class now needs about the Vanguard's forge level (+5 Salt Sea, +6 Whitefang, +7 the
  Spire at level 40), guarded by `ClassBalanceTests.Late_map_bosses_ask_every_class_for_about_the_same_forge_level`;
  the early push pace and aimed-play tests still hold (the Wraithsworn's weak point stays 700: at 600 an early loop
  outlasts the 20 minute cap). Duels are class-neutral, so PvP is unchanged. Rules changed on both sides: a client
  older than this build sends loop reports the server no longer matches, so testers need the new build.

## Done 25 Sep 2026 (the Campaign Trail)

- The Campaign Trail (decision row above). Rules: `Rules/Trail.cs` (`CampaignTrail`, `TrailProgress`, `TrailReward`,
  `TrailSeason`; `TrailTests` pin the GDD's paid track, the Khan's Alloy every 10 free tiers, the season dates, the XP
  pace and the Amber prices). Seasons are counted from `CampaignTrail.Epoch` (Mon 21 Sep 2026, 20:00 server time, a
  bounty week start); a season's name, costume and mount come from `CampaignTrail.Themes` (a season past the list
  repeats the last one, so season 2's pieces are needed before 16 Nov 2026). Stored per character in `Account.Trail`
  ("season|xp|pass|free bits|paid bits", migration `CampaignTrail`). Server: `GameService.Trail.cs`; claiming a bounty
  adds its Trail XP (`ClaimBountyAsync`); `POST /v1/trail/claim` (tier, 0 for all), `POST /v1/trail/buy` (plus);
  `/v1/dev/trail?xp=&lastSeason=` on Development. A season left behind hands its ready rewards over at the next claim or
  bounty (`RollTrail`); `TrailDto.Owed` counts them. Season pieces go through the wardrobe drops (`Hold`, worn at once
  if the slot is empty). Amber spending (the Caravan, packs, the Trail) now locks the login row first
  (`LockLoginAsync`, FOR UPDATE in a transaction): Amber is shared by up to four characters, and two of them buying at
  once could both have spent the same Amber. Ledger lines longer than their 512-character column are clipped (a
  claim of all 100 rewards failed the save before the tiers were written as ranges). `tools/smoke-trail.sh`.
  Client: `TrailPanel` (the season, the hero turning in the season costume on a `HeroStage`, tier and XP bar, THE
  TRAIL / PLUS buttons with a confirm, 50 rows of free and paid rewards, tap a row or CLAIM ALL), a round waystone
  button on the HUD under the Caravan (TIER n, or CLAIM when a reward waits), CAMPAIGN TRAIL on the bounty board
  (each bounty line shows its Trail XP). `HeroStage.Init` takes a spot so two stages never share one. Screenshot
  switch `-trail`. Art: the Amber Road Regalia for the four classes (`docs/concept/skins/<class>-AmberRoad.jpg`, Tripo
  multiview, `Skin_AmberRoad` and `<Class>_SkinAmberRoad`), the Amber Road Courser (the warhorse with teal barding on a
  black coat, `MountWarhorseAmber`, a warm glow), Caravan cards for both, the Trail's scene (`Scenes/Trail.jpg`) and
  icon (`Icons/Trail.png`). The Caravan lists the two pieces as "Campaign Trail" (not sold).

## Done 25 Sep 2026 (maps 5 and 6)

- The Cinder Marches and Whisperwood (owner, 25 Sep 2026, the second of the four picks), from the world bible's map
  table: map 5, the Cinder Marches (fire land, levels 40-50: ash fiends, magma hounds, flame cultists; Cinder Heart;
  Azhdar the Furnace Wyrm) and map 6, Whisperwood (ghost forest, levels 50-58: the hollowed dead, hanging spirits,
  lantern wisps; Whisper Bark; the Lantern Widow). The campaign is 60 stages (`Content.Maps`). The stage curve simply
  continues: the Vanguard, Kestrel and Drumcaller need about +7 for Azhdar and +8 for the Lantern Widow (at the +9 cap,
  she is the wall for now); the Wraithsworn needs about one forge level more at both, as at Whitefang (class shapes were
  left alone: changing them again breaks loop replays for the testers' current build). Guarded by
  `StageAndGearTests.Each_map_boss_is_a_power_check` (5 and 6) and
  `ClassBalanceTests.The_fifth_and_sixth_map_bosses_ask_every_class_within_a_forge_level`. Map bosses in the campaign
  have no special mechanic (as before). Art, made on fal.ai now that it has balance again (`tools/art/fal.py`: GPT
  Image 2.5 for sheets and backdrops, ESRGAN x2 for the sheets, Tripo H3.1 multiview for meshes; it retries through
  network drops and prints each job's result URL, `fetch` resumes one): turnaround sheets in `docs/concept/mobs/`
  (crops in `crops/`), meshes in `art/blender/mobs/`, built by `looks.mob_model(..., yaw_degrees=-90)`: AshFiend
  (biped, sword swing), MagmaHound (quadruped), FlameCultist (biped, staff), HollowedDead (biped, sword),
  HangingSpirit (not rigged: it floats on the lane's procedural bob), LanternWisp (biped, staff), and the bosses
  Azhdar (serpent) and LanternWidow (biped, staff). The Cinder Marches' creatures glow warm, Whisperwood's spirits
  cold (`RenderingSetup.EnsureMobs`). Backdrops `Backdrops/CinderMarches.jpg` and `Whisperwood.jpg` (also
  `docs/concept/env-cindermarches.jpg`, `env-whisperwood.jpg`) with their own ground tints. `LaneView`: `CinderMobs`,
  `WhisperMobs`, the two bosses, attack sounds.

## Done 25 Sep 2026 (direct trade)

- Direct trade (decision row above). Rules: `Rules/Trade.cs` (`DirectTrade`, `TradeState`, `TradeStep`; `TradeTests`).
  Server: `GameService.Trade.cs`, table `Trades` (`TradeSession`: both sides' item ids, sorn and step, `ChangedUtc` for
  the 5 second hold, `TouchedUtc` for the idle close) and `Items.TradeId` (migrations `DirectTrade`, `TradePieces`).
  `GET /v1/trade`, `POST /v1/trade/invite|accept|cancel|offer|press` (offer puts the whole offer each time; press locks,
  then confirms). A piece on the table has `TradeId` set and is out of the bag (`Item.OutOfBag`), so it cannot be forged,
  worn, listed or trimmed by a full bag while the other side looks at it (found in testing: a full bag's loot trim
  deleted an offered piece); cancelling, the idle close and the trade itself put it back. Every action locks the trade
  row; the exchange also locks the offered pieces (FOR UPDATE), checks them again, moves this side's sorn on its tracked
  row and the other side's in one guarded UPDATE (`Sorn >= offered`), and writes a `trade-done` ledger line for each.
  `/me` and the heartbeat carry `Trade` (a brief: an invitation or an open window). `tools/smoke-trade.sh` (needs a
  Development server). Client: `TradePanel` (ask by name; answer; the window: their offer, your offer, a sorn field,
  your bag; every row opens a stat card with the piece's stats, what wearing it would change, etchings and sockets, and
  OFFER IT / TAKE BACK for your own; LOCK OFFER / CONFIRM with the hold counted down; CANCEL TRADE; polls every 1.5 s),
  DIRECT TRADE on the Salt Exchange, a call on the HUD when someone asks or a window is open. Screenshot switches
  `-trade`, `-tradecard`.

## Done 25 Sep 2026 (more dungeons)

- Silkmother's Warren and the Carvers' Archive (decision row above). Rules: `Rules/Dungeons.cs` (`DungeonPause` None /
  Smith / RuneLock on `DungeonDef`, `FloorName`, `Riddles` and `RiddleFor(runId)`: twelve original riddles, the run's
  riddle and rune order follow from its id so the client draws the same, `WardenChest(..., dungeon, vaultOpen)`,
  `TopFloorPercent`); `Inventory.MastersNeedles` and `EtchingActions` (the fifth etching takes a Master's Needle);
  `DungeonTests`, `BountyTests`. Server: `Account.MastersNeedles`, `Account.DungeonPausedId` (the waiting run's
  dungeon; migration `MoreDungeons` marks runs already at the smith as the Spire's), `DungeonSmithRequest.Rune`, the
  run's `Pause`, `Riddle` and `Runes` in `DungeonResultDto`, `/v1/dev/gear?level=&upgrade=` (Development: a full worn
  Rare set, for late dungeons and maps). `tools/smoke-dungeon.sh` now also clears the Warren and opens the Archive's
  vault. Client: three dungeon cards on ZONES (`Thumbs/Dungeon<letters of the name>`), `RuneLockPanel` (the riddle and
  three runes, LEAVE IT SHUT), CONTINUE reopens the right pause, the Warren's floors named by level, Master's Needles
  on the Forge's ETCH and the bounty board. Art on fal.ai: the Silk Spider and the Silkmother (quadruped rigs), the
  Stone Sentinel (biped, glaive) and the Last Carver (biped, staff swing: his chisel-hammer), sheets in
  `docs/concept/mobs/`, meshes in `art/blender/mobs/`; backdrops `SilkWarren` and `CarversArchive`; the Warren also
  fields bleached scorpions and ghouls, the Archive cold wights and dead vault thieves (tinted existing mobs).
  Screenshot switches `-dungeon <id>`, `-runelock`.

## Done 25 Sep 2026 (lane floors)

- A painted floor under every lane (decision row above): `Resources/Floors/<backdrop key>.jpg`, top-down tileable
  textures drawn with each backdrop as the style reference (Higgsfield GPT Image 2.5), made seamless by a narrow edge
  crossfade (road floors only left to right, so the road is not ghosted at the edges); the candidates and the picks are
  in `docs/concept/floors/`. `LaneView.RefreshFloor` puts the backdrop's floor on the ground (tile 6 m along the lane,
  12 m deep, which offsets the low camera and leaves one road, under the hero) and scrolls it with the run; the plain
  colour and stripes stay as the fallback. The scroll direction and the road's offset are read from the ground cube's
  own top-face UVs (`MeasureFloorMapping`). `RenderingSetup.EnsureFloors` imports them repeating, anisotropic, 1024 px;
  `RenderingSetup.RenderFloors` renders every backdrop with its floor or candidates (`ORSUUN_FLOOR_KEYS`,
  `ORSUUN_FLOOR_VARIANTS=_A,_B` or `chosen`) to `artifacts/floor-*.png`.

## Done 25 Sep 2026 (maps 7 and 8, looks for levels 60-79)

- The Bloodbirch and the Drowned Steppe (decision row above): `Content.Maps` 7 and 8 (80 stages), `MapDef.BossPercent`
  (105 for the Coil Mother). Tests: `StageAndGearTests` (power checks for maps 7 and 8),
  `ClassBalanceTests.The_seventh_and_eighth_map_bosses_ask_for_epic_gear`. Art made on Higgsfield (the owner's Ultra plan)
  with Tripo H3.1 on fal: RedTreant (biped), BirchStalker (quadruped), SapHorror (not rigged, glows), Rootfather (biped,
  3.4 m), MarshSerpent (serpent), BogRider (quadruped: the drowned rider sits on the horse's mesh), LeechSwarm (not
  rigged), CoilMother (serpent, 3.4 m); sheets in `docs/concept/mobs/`, meshes in `art/blender/mobs/`; backdrops
  `Bloodbirch`, `DrownedSteppe`; floors picked by the owner. `LaneView`: `BloodbirchMobs`, `DrownedMobs`, the two
  bosses, sounds.
- Item looks for bands 6 and 7 (item levels 60-79): `Armor_T6`/`Armor_T7` (Emberplate, the Oathsworn Harness; from
  turnaround sheets of the same Vanguard, Tripo, `looks.armor_look(..., yaw_degrees=-90)`: rigged, glaive pole cut),
  `Weapon_T6`/`Weapon_T7` (the Emberwake and Oathkeeper Glaives, `looks.weapon_look`), and `Kestrel_T6/T7`,
  `Wraithsworn_T6/T7`, `Drumcaller_T6/T7` (`looks.class_look`). Sheets in `docs/concept/looks/`, sources
  `art/blender/<class>_T6|7-tripo.glb` and `look-weapon-t6|7-tripo.glb`. Bands 8-10 still show band 7.
  RenderPreview renders bands 3-7 (`artifacts/hero-T*.png`, `<class>-T*-Idle.png`).

## Done 26 Sep 2026 (maps 11 and 12, looks for levels 100-105)

- Decision row above. Rules: `Content.Maps` 11 and 12 (120 stages); the zones moved from 101-121 to 201-221
  (`Content.FirstZoneId` 201, named constants `Content.EmberSteppe` .. `Content.GorakWarCamp` used everywhere instead
  of numbers), migration `MoveZones` (moves `Accounts.ParkedStage`). Tests: `StageAndGearTests.Twelve_maps_...`,
  `ClassBalanceTests.The_last_two_map_bosses_ask_for_the_best_gear`. Old clients park by the old zone numbers, so
  install the new build with the deploy.
- Art on Higgsfield: sheets with gpt_image_2_5 (the Vanguard sheet and Hurm's as style references; the class looks
  from each class's band 9 sheet), Tripo H3.1 multiview (front/side/back crops; note: pass `face_limit: 40000` or
  the GLB comes back at ~1.4M faces and 45 MB; these were decimated to 40k in headless Blender before keeping them),
  then `looks.mob_model(..., yaw_degrees=-90)` in headless Blender (`Blender -b -P script.py`): RisenTrooper (biped,
  glaive chop), RisenCaptain (biped, sword), Varkesh (biped, sword, 3.6 m), ThroneGuard (biped, glaive chop),
  KhanShadow (biped, sword, 3.6 m), RisenRider and KhanHound (quadrupeds), OathChanter (not rigged, floats); sheets
  in `docs/concept/mobs/`, meshes in `art/blender/mobs/`. Band 10: `Armor_T10`, `Weapon_T10` (armor_look /
  weapon_look), `Kestrel_T10`, `Wraithsworn_T10`, `Drumcaller_T10` (class_look), sheets in `docs/concept/looks/`.
  Backdrops `ThousandMarkers`, `HollowThrone` (`docs/concept/env-*.jpg`), floors in `Resources/Floors` (candidates
  in `docs/concept/floors`). `LaneView`: `MarkersMobs`, `ThroneMobs`, the two bosses, sounds; the risen glow violet,
  the Khan's court gold (`RenderingSetup.EnsureMobs`). RenderPreview renders every band up to 10.

## Done 26 Sep 2026 (private messages)

- Decision row above. Rules: `Whispers` in `Chat.cs` (page 50, 1,000 kept a conversation, 60 conversations listed,
  report channel "w:"). Server: migration `PrivateMessages` (table `PrivateMessages`: from, to, text, time, read),
  `GameService.Whispers.cs`, `GET /v1/whispers` (conversations and the unread count), `GET /v1/whispers/thread`
  (`id` or `name`, `after` to poll, `before` to page back; marks what it shows as read), `POST /v1/whispers/send`,
  `POST /v1/whispers/report`; the state's `whispers` is the unread count; deleting a hero removes its messages.
  `tools/smoke-whispers.sh [url]` walks it with two throwaway heroes and deletes them.
- Client: `MessagesPanel` (list and conversation views; `-messages` and `-messages -messagesto <name>` for
  screenshots), `ServerLink` whispers calls, MESSAGE in CHAT's and FRIENDS' name actions, MESSAGES in MENU, the HUD call.

## Done 26 Sep 2026 (five skills a class, their effects and animations, mounts, the lower lane)

- Decision rows above. Rules: `SkillKind` Empower, Charge, Poison, Execute, Bind, Shield, Focus, Ward in `LaneSim`
  (timed lane states: `EmpowerActive` .. `WardActive`; `Mitigate` softens and veils blows and queues the Ward's
  returns, applied after the enemies' turn through `Wound`; poison ticks cannot crit; a cleared pack drops poison and
  chains), `SkillDef.UnlockLevel` (`FourthSkillLevel` 30, `FifthSkillLevel` 60), `HeroStats.Level` and `Mounted`
  (from the worn wardrobe), `LaneSim.IsUnlocked`, `SkillGrades.Slots` 5, `Books.Count` 20 (id = class * 5 + slot),
  `Content.BossHpWithFourthSkill` / `WithFifthSkill`, `HuntYield.Settle` without the skills' share when mounted
  (`SkillKitTests`; `ClassBalanceTests` and `StageAndGearTests` hold). Server: migration `FiveSkills` (grades, reads,
  rests, scroll stacks, listings and trade tables renumbered; `SkillReads` 512 long), `/v1/skills/train` refuses a
  skill below its level, `WearAsync` settles and reseeds the lane (a worn piece changes the hero).
- Client: `CastClips` (the 20 cast animations as keys in rig.py's form, built at run time as legacy clips "Cast0".."Cast4"
  on every hero model: a Blender pose rotation lands mirrored in x, measured on the imported models), `SkillFx` (each
  skill's effect at its clip's strike frame, the grade's layers, the auras while buffs last, the Peerless spirit from
  `LaneView.BuildSpirit`; textures in `Resources/Fx`, made on Higgsfield white on black plus three procedural ones),
  `LaneView` casts, blink, blade glow, veil and ward flashes, the HUD's five skill buttons (locked ones show their level;
  RIDING while mounted) and DISMOUNT / MOUNT UP, the SKILLS screen's five cards, eight new skill icons. Dev switch
  `-castshow <slot> [-castdelay s]` shoots a cast just after its strike.

## Done 26 Sep 2026 (skill grades, Technique Scrolls, Honor, the inventory screen)

- Decision rows above. Rules: `SkillGrades.cs` (`Books`: ids, names, the class and slot of a book; `SkillGrades`:
  tiers, names, bonus, reads needed, Honor cost, chance, rest, `Problem`, `Train`, `ForClass`, `Parse`/`Format`),
  `HeroStats.SkillGradeBonusPercent` used by `LaneSim.Cast` (Burst and Area power, Haste duration),
  `HeroFactory.FromEquipment(..., skillGrades)`, `PlayerSession.SetSkillGrades` (all twelve; the hero takes its class's
  three), `Inventory.Books`, a scroll in every Warden chest, the Hunt Marks shop's Technique Scroll (`HuntShop.Buy(...,
  cls, rng)`), the Pit shop's (`PitGood.TechniqueScroll`), `DirectTrade.FormatBooks`/`ParseBooks` (`SkillGradeTests`).
  Server: `GameService.Skills.cs` (`/v1/skills/train`: a read or an Oathstone try for a slot of the class played; a grade
  that rises settles and reseeds the lane; Peerless is said in world chat; Development `/v1/dev/skill?book&grade`),
  `Account.SkillGrades` / `SkillProgress` / `SkillReads` (twelve values each, by book id) and `Account.Honor`, table
  `BookStacks` (one row per hero and book; other heroes' stacks change by an upsert, `AddBooksElsewhereAsync`),
  `MarketListing.BookId` (-1 for a piece) and `BookCount`, `TradeSession.FromBooks`/`ToBooks` (migration
  `SkillBooksHonor`). The Exchange lists scroll stacks (a count for one price; `/v1/market?books=true`); a direct trade
  carries them and checks both sides' stacks at the exchange, the other side's under `FOR UPDATE`. Honor: Korstones in
  `Settle`, Pit wins, Wardens. The dev grant adds five of each of the class's scrolls and 500 Honor.
  `tools/smoke-books.sh` (local: a read, an Oathstone try, a stack sold on the Exchange, one traded).
- Client: `GearPanel` rebuilt as INVENTORY (mockup D; `-gear`, `-bagtab n` for a tab, `-bagcard` for the first tile's
  card), `HeroStage.Zoom`, `SkillsPanel` (`-skills`; each try asks first), the hunt's skill names show the grade and
  open SKILLS, `MarketPanel` BOOK filter, scroll listings and a count in the price box (`OpenSell`, `OpenSellBook`;
  `-marketbooks`, `-sellbook`, `-sell`, `-mylistings`), `TradePanel` scroll offers with a count (scrolls first in the
  bag list). `ServerLink.TrainSkill` completes with the result; a scroll listing sends the empty Guid as `itemId`.

## Done 25 Sep 2026 (Pit seasons)

- Decision row above. Rules: `Pits` seasons (`SeasonKey`, `SeasonMinFights`, `SeasonLaurels`, `SeasonReward`, `Title`,
  `SoftReset`) and `PitGood`/`PitShopItem.GrantTo` for the wider shop (`PitsTests`). Server: `Account.PitSeason`,
  `PitSeasonWins/Losses`, `PitLastSeason/Rank/Rating/Laurels`, `PitTitle`, table `PitSeasons` (one row per settled
  season: its key is unique, so a settlement claims it once; the champions) in migration `PitSeasons`;
  `SettlePitSeasonAsync` runs in `TickWorldAsync`. Its first run ever only marks last week settled and opens this week's
  season for heroes who fought before seasons existed (their lifetime record becomes this season's). The dev grant adds
  100 Laurels; Development `/v1/dev/pit-season-end` settles the running season at once (it resets everyone's rating
  halfway: local servers only, `tools/smoke-pitseason.sh` refuses any other URL). Client: `PitsPanel` season line (end,
  last season's rank and Laurels or champions, the title held), two shop rows (icons `Oathstone`, `PinningWax` made on
  Higgsfield in the icon set's style), the season's board with titles.

## Done 25 Sep 2026 (Banner change and Oath Renewal)

- Decision row above. Rules: `Banners.ChangeOathstones` and `ChangeProblem`, `OathRenewal` (in `Banners.cs`),
  `Inventory.Oathstones`, `HeroFactory.FromEquipment(..., renewals)`, `PlayerSession.Renewals`/`SetRenewals`, the Last
  Carver's Oathstones in `Dungeons.WardenChest` (`OathTests`). Server: `GameService.Oaths.cs` (`/v1/banner/change`
  under the login row lock, `/v1/renew`, Development `/v1/dev/level?level=n`; the dev grant adds 5 Oathstones),
  `Account.Oathstones`, `Account.Renewals`, `Login.BannerChangedSeason` (migration `OathsAndBannerChange`); the state
  carries `Renewals` and `Inventory.Oathstones`. `tools/smoke-oaths.sh`. Client: `BannerOath.OpenChange` (`-oathchange`),
  the War screen's CHANGE, the Gear screen's hero line (renewals shown, RENEW YOUR OATH at 105, a dialog explains it
  otherwise), Oathstones on the bounty board's materials line. The Gear screen's "if worn" now counts class, wardrobe
  and renewals (it compared a bare Vanguard with the real hero).

## Done 25 Sep 2026 (maps 9 and 10, looks for levels 80-99)

- Decision row above. `Content.Maps` 9 and 10 (100 stages), `FirstZoneId` 101. Tests: `StageAndGearTests`
  (`Ten_maps_...`, stage 100 is not a zone), `ClassBalanceTests.The_ninth_and_tenth_map_bosses_ask_for_the_maps_epic_gear`.
  Art on Higgsfield: sheets (gpt_image_2_5 with the vanguard sheet as reference), Tripo H3.1 multiview through
  Higgsfield (crops staged briefly on the playtest server's downloads folder and imported by URL, then deleted), then
  `looks.mob_model`: StoneGiant, BonePicker, Hurm, KhanCultist, GildedConstruct, MerchantPrince (bipeds), SiegeBeast
  (quadruped), DebtWraith (not rigged, floats); sheets in `docs/concept/mobs/`, meshes in `art/blender/mobs/`;
  backdrops `ColossusGraves`, `SunkenBazaar`; floors in `Resources/Floors` (candidates in `docs/concept/floors`).
  `LaneView`: `GravesMobs`, `BazaarMobs`, the two bosses, sounds; Hurm and the debt wraiths glow cold.
- Looks for bands 8 and 9: `Armor_T8/T9`, `Weapon_T8/T9`, `Kestrel|Wraithsworn|Drumcaller_T8/T9` (sheets in
  `docs/concept/looks/`). RenderPreview renders bands up to 9. Band 10 still shows band 9.
- `-stage <n>` (local screenshots) now also gears the hero for the stage (Epic +9 of its item level) so the fight stays
  on screen. `tools/art/fal.py` retries a 403/429 submission.

## Done 25 Sep 2026 (friends, chat actions, guild invites)

- Decision row above. Rules: `Friends` (in `Chat.cs`: 50 friends, 20 waiting, online under 2 minutes, `Seen`),
  `Guilds.MaxInvites` 20 and `InviteDays` 3 (`SocialTests`). Server: `GameService.Friends.cs` (`FindHeroAsync` finds a
  hero by id or name and is shared with trade and guild invites; `/v1/friends`, `/friends/add|answer|remove`), guild
  invites in `GameService.Guilds.cs` (`/v1/guild/invite`, `/guild/invite/answer`, under the guild row lock), tables
  `Friendships` and `GuildInvites` (migration `FriendsAndInvites`); `/v1/trade/invite` also takes an `accountId`;
  `/me` and the heartbeat carry `FriendAsks` and `GuildInvites`. Deleting a hero removes its friendships and invites;
  blocking removes a friendship. `tools/smoke-friends.sh`. Client: `FriendsPanel` (MENU > FRIENDS, CHAT > FRIENDS;
  `-friends`), the chat name menu (`-chatpick`), the guild screen's INVITES popup (no guild; opens by itself the first
  time an invite shows) and the invite field under ASKING / INVITE (`-guildpopup`), the HUD's call.
- Fixed on the way: the guild screen without a guild threw every frame (its colour swatches looked for an `Outline`
  the kit's framed buttons no longer have), so the found-a-guild colour never showed as chosen.

## Done 25 Sep 2026 (the way in)

- "The way in" (decision row above). `AccountPanel.FirstScreen` (not chosen on this phone, no email or link): the title
  gives way by itself once the lobby answers (no TAP TO BEGIN while connecting), the sign-in screen opens under the
  fading title; SIGN OUT clears `orsuun.accountChosen`, so the phone goes back to it. `GameRoot.LateUpdate` runs the way
  in (after the frame's coroutines, so a screen closed by a server answer is replaced in the same frame);
  `ServerLink.Connected` (a hero played, or the lobby loaded: the account screen used to say "Offline" at the character
  screen) and `WaitingForHero` (signing in or at the character screen: the character screen covers the lane).
  `BannerOath` swears through `/v1/lobby/banner` at the character screen (`GameService.SwearLoginAsync`, under the
  login row lock; the in-game `/v1/banner` stays for older accounts). Texts speak of the account and its heroes.
  `tools/smoke-characters.sh` swears at the character screen. Screenshots: `-firstrun [guest|oath]` runs the way in
  under `-shot` (with a fresh `orsuun.deviceToken`), playing as guest and swearing to the Sky Banner.
- The character screen's hero turns under a finger (owner, 25 Sep 2026: "add turning characters with sliding with hand
  as well"): `HeroStage` (also the Campaign Trail's showcase) takes a drag on its picture (a drag across 1.1 screen
  widths is a whole turn), spins on a little after letting go, and after 3 s untouched turns back to the front and
  sways again. Screenshots: `-dragturn <px>` drags through the event system two seconds after a hero shows (the log
  names what the raycast hit).

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

0. Database backups (owner, 25 Sep 2026: "put backup in mind, i will buy it later", meaning the Hetzner Storage Box):
   nothing backs up the playtest database yet beyond the one-off copy on the Mac (`~/orsuun-backups`). The
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
   Korshard icons, Banner emblems, Forge VFX boards, store screenshots) is the checklist in GDD section 14.1. Done since: bands 3-10
   of item looks (all eleven), boar and deserter sheets and models, a first UI colour pass. (The Mirage
   Queen, mob rigs and the other three classes are done: see the sections above.)
3. Later: second class (Wraithsworn Voidpact), Bannerkin companion, sixth etching, Temper, Oath Renewal (GDD section 12),
   real shared boss HP pools, Hunt Marks and the Hearthfire subscription (Bulk Turn's 10/50 split depends on it).

## Known gaps

- `ORSUUN_RESET_DB=1` wipes the schema on a Development start; keep it out of any shared environment.
- `tools/smoke.sh` against the live server swears a Banner, sieges Stagfort and scores points in the shared world.
  Reset after it: `DELETE FROM "BannerScores"; UPDATE "Fortresses" SET "Wall"="WallMax", "SiegeEmber"=0, "SiegeSky"=0, "SiegeGold"=0;`
  (through `docker compose -f deploy/docker-compose.yml --env-file deploy/.env exec -T postgres psql -U orsuun -d orsuun -c '...'`).
- The fifth etching needs a Master's Needle, which nothing sells or drops yet (a Caravan item in the GDD; the Carvers'
  Archive dungeon is meant to be its main source).
- Players are shown to each other by a generated name; custom names need moderation first. Guild names and tags go
  through a short word filter (`Guilds.Clean`) only; reports and a review queue are needed before a public launch.
- Guilds: invites exist (25 Sep 2026); guild war and fortress keeps are asynchronous first versions (no live 20v20 / 50v50, no Free
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
