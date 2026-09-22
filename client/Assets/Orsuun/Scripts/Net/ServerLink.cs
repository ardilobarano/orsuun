using System;
using System.Collections;
using System.Text;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.Networking;

namespace Orsuun.Client.Net
{
    /// <summary>
    /// Talks to Orsuun.Server. When the server is reachable, every Forge and Turnstone roll comes from it and
    /// heartbeats settle hunting time; when it is not, the game runs in local mode with a visible banner.
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
        public string Status { get; private set; } = "connecting";
        public SettlementDto LastSettlement { get; private set; }

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
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-server");
            if (i >= 0 && i + 1 < args.Length) _baseUrl = args[i + 1];
            StartCoroutine(Run());
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
                yield return Post("/v1/heartbeat", "{}", true, json =>
                {
                    StateDto state = JsonUtility.FromJson<StateDto>(json);
                    if (state.settlement != null && state.settlement.countedSeconds > 0) LastSettlement = state.settlement;
                    Apply(state);
                }, error => Status = "LOCAL MODE: " + error);
                yield return new WaitForSecondsRealtime(HeartbeatSeconds);
            }
        }

        /// <summary>Server Forge. Completes with the result, or with null and an error message.</summary>
        public IEnumerator Forge(ForgeMethod method, Action<ForgeResultDto, string> done)
        {
            var req = new ForgeRequest { requestId = Guid.NewGuid().ToString("N"), method = method.ToString() };
            ForgeResultDto result = null;
            string failure = null;
            yield return Post("/v1/forge", JsonUtility.ToJson(req), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                Apply(state);
                result = state.lastForge;
            }, error => failure = error);
            done(result, failure);
        }

        public IEnumerator Turn(Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/turn", JsonUtility.ToJson(new TurnRequest { requestId = Guid.NewGuid().ToString("N") }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        public IEnumerator DevGrant()
        {
            yield return Post("/v1/dev/grant", "{}", true, json => Apply(JsonUtility.FromJson<StateDto>(json)), _ => { });
        }

        private void Apply(StateDto s)
        {
            var inventory = new Inventory
            {
                Sorn = s.inventory.sorn, Potions = s.inventory.potions, Materials = s.inventory.materials,
                ScrollsOfMercy = s.inventory.scrollsOfMercy, KhansAlloys = s.inventory.khansAlloys,
                AnvilWards = s.inventory.anvilWards, Turnstones = s.inventory.turnstones,
            };
            var weapon = new ItemState(s.weapon.itemLevel, (Rarity)Enum.Parse(typeof(Rarity), s.weapon.rarity))
            {
                UpgradeLevel = s.weapon.upgradeLevel, PatienceBp = s.weapon.patienceBp, LockedEtchingIndex = s.weapon.lockedEtchingIndex,
            };
            foreach (EtchingDto e in s.weapon.etchings) weapon.Etchings.Add(new Etching(e.entryId, e.tier, e.value));
            _player.ApplyRemote(inventory, weapon, s.weaponsBroken);
            Online = true;
            Status = "server: " + _baseUrl;
        }

        private IEnumerator Post(string path, string body, bool auth, Action<string> ok, Action<string> fail)
        {
            using var req = new UnityWebRequest(_baseUrl + path, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (auth) req.SetRequestHeader("X-Session", _session);
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                ok(req.downloadHandler.text);
                yield break;
            }

            Online = false;
            string message = req.responseCode >= 400 && req.downloadHandler.text.Length > 0
                ? JsonUtility.FromJson<ErrorDto>(req.downloadHandler.text).message
                : req.error;
            fail(message);
        }

        // JsonUtility mirrors of the server contracts. Enums travel as strings.
        [Serializable] public class GuestLoginRequest { public string deviceToken; }
        [Serializable] public class GuestLoginResponse { public string accountId; public string sessionToken; public bool created; }
        [Serializable] public class ForgeRequest { public string requestId; public string method; }
        [Serializable] public class TurnRequest { public string requestId; }
        [Serializable] public class ErrorDto { public string code; public string message; }
        [Serializable] public class EtchingDto { public int entryId; public string name; public int tier; public int value; }
        [Serializable] public class WeaponDto { public string id; public int itemLevel; public string rarity; public int upgradeLevel; public int patienceBp; public int lockedEtchingIndex; public EtchingDto[] etchings; }
        [Serializable] public class InventoryDto { public long sorn; public int potions; public int materials; public int scrollsOfMercy; public int khansAlloys; public int anvilWards; public int turnstones; }
        [Serializable] public class SettlementDto { public long countedSeconds; public long packs; public long korstones; public long sornEarned; public bool offline; }
        [Serializable] public class ForgeResultDto { public string outcome; public int chanceBp; public int levelBefore; public int levelAfter; }
        [Serializable] public class StateDto { public string accountId; public InventoryDto inventory; public WeaponDto weapon; public int weaponsBroken; public SettlementDto settlement; public ForgeResultDto lastForge; }
    }
}
