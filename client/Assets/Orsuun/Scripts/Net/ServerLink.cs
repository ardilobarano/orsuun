using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.Networking;

namespace Orsuun.Client.Net
{
    /// <summary>
    /// Talks to Orsuun.Server. When the server is reachable, every roll comes from it and heartbeats settle
    /// hunting time; when it is not, the game runs in local mode with a visible banner.
    /// </summary>
    public sealed class ServerLink : MonoBehaviour
    {
        public const string DefaultBaseUrl = "http://localhost:5080";
        private const float HeartbeatSeconds = 30f;
        private const string DeviceTokenKey = "orsuun.deviceToken";

        private string _baseUrl = DefaultBaseUrl;
        private string _session;
        private PlayerSession _player;

        public bool Online { get; private set; }
        /// <summary>The server's address (the privacy policy lives at BaseUrl + "/privacy").</summary>
        public string BaseUrl => _baseUrl;
        public string Status { get; private set; } = "connecting";
        public SettlementDto LastSettlement { get; private set; }
        /// <summary>Server item ids for the ItemState instances currently in the session, needed by Equip.</summary>
        public Dictionary<ItemState, string> ItemIds { get; } = new Dictionary<ItemState, string>();

        public void MarkLocal() => Status = "LOCAL MODE (-local)";

        /// <summary>Stops all traffic and falls back to local rolls. Used by tests and by the -local switch.</summary>
        public void Disconnect()
        {
            StopAllCoroutines();
            _session = null;
            Online = false;
            Status = "LOCAL MODE";
        }

        public void Init(PlayerSession player)
        {
            _player = player;
            _baseUrl = ResolveBaseUrl();
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run());
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;

        private const int CrashReportsPerRun = 10;
        private readonly HashSet<string> _reported = new HashSet<string>();

        /// <summary>
        /// Crash reports: every uncaught exception goes to the server once per message (a few per run at most, and
        /// the server caps an account at 20 an hour). Only the message, the stack, the platform and the version are
        /// sent; the privacy policy lists them.
        /// </summary>
        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception || _session == null || _reported.Count >= CrashReportsPerRun || !_reported.Add(message)) return;
            var body = new ClientLogRequest { platform = Application.platform.ToString(), version = Application.version, message = message, stack = stack };
            StartCoroutine(Send("POST", "/v1/client-log", JsonUtility.ToJson(body), true, _ => { }, _ => { }));
        }

        /// <summary>
        /// Deletes the account and everything on it from the server, then signs in again as a new guest on a new
        /// device token (a fresh start). Completes with null, or an error message.
        /// </summary>
        public IEnumerator DeleteAccount(Action<string> done)
        {
            if (_session == null)
            {
                done("Not connected to the server.");
                yield break;
            }
            string failure = null;
            yield return Send("DELETE", "/v1/account", null, true, _ => { }, error => failure = error ?? "No answer from the server.");
            if (failure != null)
            {
                done(failure);
                yield break;
            }
            StopAllCoroutines();
            _session = null;
            Online = false;
            Status = "connecting";
            PlayerPrefs.DeleteKey(DeviceTokenKey);
            PlayerPrefs.Save();
            StartCoroutine(Run());
            done(null);
        }

        /// <summary>-server on the command line, else Resources/server-url.txt (written by the build script), else localhost.</summary>
        public static string ResolveBaseUrl()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-server");
            if (i >= 0 && i + 1 < args.Length) return args[i + 1].TrimEnd('/');

            var asset = Resources.Load<TextAsset>("server-url");
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text)) return asset.text.Trim().TrimEnd('/');

            return DefaultBaseUrl;
        }

        private IEnumerator Run()
        {
            string token = PlayerPrefs.GetString(DeviceTokenKey, "");
            if (token.Length == 0)
            {
                token = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(DeviceTokenKey, token);
            }

            yield return Post("/v1/auth/guest", JsonUtility.ToJson(new GuestLoginRequest { deviceToken = token }), false,
                json => { _session = JsonUtility.FromJson<GuestLoginResponse>(json).sessionToken; },
                error => Status = "LOCAL MODE: " + error);

            if (_session == null) yield break;

            while (true)
            {
                List<LoopReport> reports = _player.TakeReports();
                bool sent = false;
                yield return Post("/v1/heartbeat", HeartbeatBody(reports), true, json =>
                {
                    sent = true;
                    StateDto state = JsonUtility.FromJson<StateDto>(json);
                    if (state.settlement != null && state.settlement.countedSeconds > 0) LastSettlement = state.settlement;
                    Apply(state);
                }, error => Status = "LOCAL MODE: " + error);
                if (!sent) _player.RequeueReports(reports);
                yield return new WaitForSecondsRealtime(HeartbeatSeconds);
            }
        }

        /// <summary>The finished farm loops since the last heartbeat, for the server to replay (active play).</summary>
        private static string HeartbeatBody(List<LoopReport> reports)
        {
            var body = new HeartbeatRequest { loops = new LoopReportDto[reports.Count] };
            for (int i = 0; i < reports.Count; i++)
            {
                LoopReport r = reports[i];
                var casts = new CastDto[r.Casts.Count];
                for (int c = 0; c < casts.Length; c++) casts[c] = new CastDto { tick = r.Casts[c].Tick, skill = r.Casts[c].Skill };
                body.loops[i] = new LoopReportDto { loop = r.Loop, ticks = r.Ticks, potions = r.Potions, autoCast = r.AutoCast, casts = casts };
            }
            return JsonUtility.ToJson(body);
        }

        /// <summary>Server Forge. Completes with the result, or with null and an error message.</summary>
        /// <summary>Server id of an item in the session (worn or in the bag), or null.</summary>
        public string IdOf(ItemState item) => item != null && ItemIds.TryGetValue(item, out string id) ? id : null;

        /// <summary>The session's current instance of a server item (every refresh rebuilds them), or null.</summary>
        public ItemState ItemById(string id)
        {
            if (id == null) return null;
            foreach (KeyValuePair<ItemState, string> pair in ItemIds)
                if (pair.Value == id) return pair.Key;
            return null;
        }

        /// <summary>
        /// Server Forge on the piece on the anvil: by item id when known (bag pieces need it), else the piece worn in
        /// the slot. JsonUtility writes a null string as "", which the server cannot read as an id, so it is dropped.
        /// </summary>
        public IEnumerator Forge(ForgeMethod method, EquipSlot slot, string itemId, Action<ForgeResultDto, string> done)
        {
            var req = new ForgeRequest { requestId = Guid.NewGuid().ToString("N"), method = method.ToString(), slot = slot.ToString(), itemId = itemId };
            ForgeResultDto result = null;
            string failure = null;
            yield return Post("/v1/forge", WithoutEmptyItemId(JsonUtility.ToJson(req)), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                Apply(state);
                result = state.lastForge;
            }, error => failure = error);
            done(result, failure);
        }

        /// <summary>
        /// One turn, or a Bulk Turn toward a goal (up to five etchings with tiers; an empty goal runs the batch to the
        /// end). Completes with (turns, stopped, error).
        /// </summary>
        public IEnumerator Turn(int count, IReadOnlyList<TurnTarget> goal, EquipSlot slot, string itemId, Action<int, bool, string> done)
        {
            string failure = null;
            int turns = 0;
            bool stopped = false;
            var targets = new TurnTargetDto[goal.Count];
            for (int i = 0; i < targets.Length; i++) targets[i] = new TurnTargetDto { entryId = goal[i].EntryId, minTier = goal[i].MinTier };
            var req = new TurnRequest { requestId = Guid.NewGuid().ToString("N"), count = count, slot = slot.ToString(), itemId = itemId, targets = targets };
            yield return Post("/v1/turn", WithoutEmptyItemId(JsonUtility.ToJson(req)), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                Apply(state);
                if (state.lastTurn != null) { turns = state.lastTurn.turns; stopped = state.lastTurn.stopped; }
            }, error => failure = error);
            done(turns, stopped, failure);
        }

        /// <summary>Switches the class being played on the server; the state that comes back carries the new lane seed.</summary>
        public IEnumerator SetClass(HeroClass cls, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/class", "{\"heroClass\":\"" + cls + "\"}", true, json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        private static string WithoutEmptyItemId(string json) => json.Replace(",\"itemId\":\"\"", "");

        public IEnumerator Equip(string itemId, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/equip", JsonUtility.ToJson(new EquipRequest { requestId = Guid.NewGuid().ToString("N"), itemId = itemId }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>Server shard insert. Completes with the result text, or an error message.</summary>
        public IEnumerator SocketInsert(string itemId, int socketIndex, ShardType type, int rank, Action<bool, string> done)
        {
            bool ok = false;
            string text = null;
            var req = new SocketInsertRequest { requestId = Guid.NewGuid().ToString("N"), itemId = itemId, socketIndex = socketIndex, type = type.ToString(), rank = rank };
            yield return Post("/v1/socket/insert", JsonUtility.ToJson(req), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                Apply(state);
                ok = state.lastSocket != null && state.lastSocket.success;
                text = state.lastSocket?.text;
            }, error => text = error);
            done(ok, text);
        }

        public IEnumerator SocketClear(string itemId, int socketIndex, Action<string> done)
        {
            string text = null;
            var req = new SocketClearRequest { requestId = Guid.NewGuid().ToString("N"), itemId = itemId, socketIndex = socketIndex };
            yield return Post("/v1/socket/clear", JsonUtility.ToJson(req), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                Apply(state);
                text = state.lastSocket?.text;
            }, error => text = error);
            done(text);
        }

        public IEnumerator Park(int stage, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/park", JsonUtility.ToJson(new ParkRequest { stage = stage }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>Server push. The result carries the seed the client replays.</summary>
        public IEnumerator Push(Action<PushResultDto, string> done)
        {
            PushResultDto result = null;
            string failure = null;
            yield return Post("/v1/push", JsonUtility.ToJson(new PushRequest { requestId = Guid.NewGuid().ToString("N") }), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                result = state.lastPush;
                Apply(state);
            }, error => failure = error);
            done(result, failure);
        }

        public IEnumerator DevGrant()
        {
            yield return Post("/v1/dev/grant", "{}", true, json => Apply(JsonUtility.FromJson<StateDto>(json)), _ => { });
        }

        public IEnumerator DevBossesUp()
        {
            yield return Post("/v1/dev/bosses-up", "{}", true, json => Apply(JsonUtility.FromJson<StateDto>(json)), _ => { });
        }

        /// <summary>Server Commander fight. The result carries the seed the client replays.</summary>
        public IEnumerator FightBoss(int bossId, Action<BossFightResultDto, string> done)
        {
            BossFightResultDto result = null;
            string failure = null;
            yield return Post("/v1/boss/fight", JsonUtility.ToJson(new BossFightRequest { requestId = Guid.NewGuid().ToString("N"), bossId = bossId }), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                result = state.lastBossFight;
                Apply(state);
            }, error => failure = error);
            done(result, failure);
        }

        /// <summary>Commander statuses from the last /me or heartbeat; empty until the first one lands.</summary>
        public BossStatusDto[] Bosses { get; private set; } = new BossStatusDto[0];
        public float BossesReceivedAt { get; private set; }
        /// <summary>The server's Evening Bell state from the last response.</summary>
        public BellDto Bell { get; private set; }
        private Bell _appliedBell = Rules.Bell.None;
        public Bell ActiveBell => _appliedBell;

        private void Apply(StateDto s)
        {
            var inventory = new Inventory
            {
                Sorn = s.inventory.sorn, Potions = s.inventory.potions, Materials = s.inventory.materials,
                ScrollsOfMercy = s.inventory.scrollsOfMercy, KhansAlloys = s.inventory.khansAlloys,
                AnvilWards = s.inventory.anvilWards, Turnstones = s.inventory.turnstones,
                EtchingNeedles = s.inventory.etchingNeedles, SummoningMarkers = s.inventory.summoningMarkers, Xp = s.inventory.xp,
            };
            if (s.inventory.korshards != null) Array.Copy(s.inventory.korshards, inventory.Korshards, Math.Min(5, s.inventory.korshards.Length));
            if (s.inventory.skins != null) inventory.Skins.AddRange(s.inventory.skins);
            if (s.bosses != null && s.bosses.Length > 0)
            {
                Bosses = s.bosses;
                BossesReceivedAt = Time.realtimeSinceStartup;
            }
            if (s.bell != null)
            {
                Bell = s.bell;
                var active = (Bell)Enum.Parse(typeof(Bell), s.bell.active);
                if (active != _appliedBell)
                {
                    _appliedBell = active;
                    _player.ApplyBell(active);
                }
            }

            // Every ItemState is rebuilt below: remember which piece is on the anvil by its server id.
            string anvilId = IdOf(_player.OnAnvil);
            ItemIds.Clear();
            var equipped = new List<ItemState>();
            foreach (ItemDto dto in s.items)
            {
                ItemState item = ToState(dto);
                ItemIds[item] = dto.id;
                if (dto.equipped) equipped.Add(item); else inventory.Loot.Add(item);
            }

            _player.ApplyRemote(inventory, equipped, s.weaponsBroken, s.highestStageCleared, s.parkedStage);
            if (anvilId != null)
                foreach (KeyValuePair<ItemState, string> pair in ItemIds)
                    if (pair.Value == anvilId) { _player.PutOnAnvil(pair.Key); break; }
            if (!string.IsNullOrEmpty(s.heroClass) && Enum.TryParse(s.heroClass, out HeroClass cls)) _player.SetClass(cls);
            // The farm lane's seed: new on login and on every park; the lane then plays seeded loops the server replays.
            if (s.lane != null && ulong.TryParse(s.lane.seed, out ulong laneSeed)) _player.SetLaneSeed(laneSeed, s.lane.loop);
            Online = true;
            Status = "server: " + _baseUrl;
        }

        private static ItemState ToState(ItemDto dto)
        {
            var item = new ItemState(dto.itemLevel, (Rarity)Enum.Parse(typeof(Rarity), dto.rarity), (EquipSlot)Enum.Parse(typeof(EquipSlot), dto.slot))
            {
                UpgradeLevel = dto.upgradeLevel, PatienceBp = dto.patienceBp, LockedEtchingIndex = dto.lockedEtchingIndex,
            };
            foreach (EtchingDto e in dto.etchings) item.Etchings.Add(new Etching(e.entryId, e.tier, e.value));
            if (dto.sockets != null)
            {
                for (int i = 0; i < dto.sockets.Length && i < item.Sockets.Length; i++)
                {
                    SocketDto s = dto.sockets[i];
                    if (s.dead) item.Sockets[i] = Socket.DeadShard;
                    else if (!string.IsNullOrEmpty(s.type)) item.Sockets[i] = new Socket((ShardType)Enum.Parse(typeof(ShardType), s.type), s.rank);
                }
            }
            return item;
        }

        private IEnumerator Post(string path, string body, bool auth, Action<string> ok, Action<string> fail) =>
            Send("POST", path, body, auth, ok, fail);

        private IEnumerator Send(string method, string path, string body, bool auth, Action<string> ok, Action<string> fail)
        {
            using var req = new UnityWebRequest(_baseUrl + path, method);
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            if (auth) req.SetRequestHeader("X-Session", _session);
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                ok(req.downloadHandler.text);
                yield break;
            }

            string message = req.responseCode >= 400 && req.downloadHandler.text.Length > 0
                ? JsonUtility.FromJson<ErrorDto>(req.downloadHandler.text).message
                : req.error;
            // A rule refusal (400/409) is an answer, not an outage; only transport failures drop to local mode.
            if (req.responseCode < 400) Online = false;
            fail(message);
        }

        // JsonUtility mirrors of the server contracts. Enums travel as strings.
        [Serializable] public class GuestLoginRequest { public string deviceToken; }
        [Serializable] public class GuestLoginResponse { public string accountId; public string sessionToken; public bool created; }
        [Serializable] public class ForgeRequest { public string requestId; public string method; public string slot; public string itemId; }
        [Serializable] public class TurnRequest { public string requestId; public int count; public string slot; public string itemId; public TurnTargetDto[] targets; }
        [Serializable] public class TurnTargetDto { public int entryId; public int minTier; }
        [Serializable] public class TurnResultDto { public int turns; public int turnstonesSpent; public bool stopped; }
        [Serializable] public class BellDto { public string active; public string activeName; public string next; public int minutesUntilNext; public string serverLocalTime; }
        [Serializable] public class EquipRequest { public string requestId; public string itemId; }
        [Serializable] public class ParkRequest { public int stage; }
        [Serializable] public class PushRequest { public string requestId; }
        [Serializable] public class ErrorDto { public string code; public string message; }
        [Serializable] public class ClientLogRequest { public string platform; public string version; public string message; public string stack; }
        [Serializable] public class EtchingDto { public int entryId; public string name; public int tier; public int value; }
        [Serializable] public class SocketDto { public bool dead; public string type; public int rank; public string text; }
        [Serializable] public class ItemDto { public string id; public string slot; public bool equipped; public string name; public int itemLevel; public string rarity; public int upgradeLevel; public int patienceBp; public int lockedEtchingIndex; public EtchingDto[] etchings; public SocketDto[] sockets; }
        [Serializable] public class SocketInsertRequest { public string requestId; public string itemId; public int socketIndex; public string type; public int rank; }
        [Serializable] public class SocketClearRequest { public string requestId; public string itemId; public int socketIndex; }
        [Serializable] public class SocketResultDto { public bool success; public int socketIndex; public string text; }
        [Serializable] public class InventoryDto { public long sorn; public int potions; public int materials; public int scrollsOfMercy; public int khansAlloys; public int anvilWards; public int turnstones; public int etchingNeedles; public int summoningMarkers; public long xp; public int level; public int[] korshards; public string[] skins; }
        [Serializable] public class BossFightRequest { public string requestId; public int bossId; }
        [Serializable] public class BossStatusDto { public int bossId; public string name; public string mechanic; public bool up; public long secondsLeft; public bool foughtThisSpawn; }
        [Serializable] public class BossFightResultDto { public int bossId; public ulong seed; public long damage; public bool killed; public int rank; public string chest; public int potionsAtStart; public string bell; }
        [Serializable] public class SettlementDto { public long countedSeconds; public long packs; public long korstones; public long sornEarned; public bool offline; public int activeBp; public int loopsVerified; }
        [Serializable] public class LaneDto { public string seed; public int loop; }
        [Serializable] public class CastDto { public int tick; public int skill; }
        [Serializable] public class LoopReportDto { public int loop; public int ticks; public int potions; public bool[] autoCast; public CastDto[] casts; }
        [Serializable] public class HeartbeatRequest { public LoopReportDto[] loops; }
        [Serializable] public class ForgeResultDto { public string outcome; public int chanceBp; public int levelBefore; public int levelAfter; }
        [Serializable] public class PushResultDto { public int stage; public bool cleared; public ulong seed; public int ticks; public int newHighestStageCleared; public int potionsAtStart; public string bell; }
        [Serializable] public class StateDto { public string accountId; public InventoryDto inventory; public ItemDto[] items; public int weaponsBroken; public int highestStageCleared; public int parkedStage; public BossStatusDto[] bosses; public BellDto bell; public SettlementDto settlement; public ForgeResultDto lastForge; public PushResultDto lastPush; public BossFightResultDto lastBossFight; public SocketResultDto lastSocket; public TurnResultDto lastTurn; public LaneDto lane; public string heroClass; }
    }
}
