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
        /// <summary>The pieces of the last state as the server sent them (bag and worn; listed and depot pieces are not in it).</summary>
        public ItemDto[] LastItems { get; private set; }

        public void MarkLocal() => Status = "LOCAL MODE (-local)";

        /// <summary>Bounties with this account's counts, from the last response (server-counted).</summary>
        public BountyBoardDto Bounties { get; private set; }
        public float BountiesReceivedAt { get; private set; }
        /// <summary>The Banner this account is sworn to (None before the oath) and its name as other players see it.</summary>
        public Banner Banner { get; private set; } = Banner.None;
        public string PlayerName { get; private set; } = "";
        /// <summary>The War of Banners, fetched on demand by the WAR screen.</summary>
        public WarDto War { get; private set; }
        public float WarReceivedAt { get; private set; }
        /// <summary>The account's guild at a glance (empty tag = no guild) and its Guild Tallies, from every state.</summary>
        public GuildBriefDto Guild { get; private set; }
        public bool InGuild => Guild != null && !string.IsNullOrEmpty(Guild.tag);
        public int Tallies { get; private set; }
        /// <summary>The GUILD screen's data, fetched on demand and returned by every guild call.</summary>
        public GuildViewDto GuildView { get; private set; }
        /// <summary>The Salt Exchange screen's data, fetched on demand and returned by every market call.</summary>
        public MarketDto MarketView { get; private set; }
        /// <summary>The account's sign-in email; empty for a guest.</summary>
        public string Email { get; private set; } = "";
        /// <summary>Google / Apple logins linked to the account ("google", "apple").</summary>
        public string[] Logins { get; private set; } = new string[0];
        public bool Registered => !string.IsNullOrEmpty(Email) || Logins.Length > 0;
        /// <summary>Whether the sign-in email is proven (a code sent to it came back, or a reset used one).</summary>
        public bool EmailVerified { get; private set; }
        /// <summary>The sign-in providers this server offers ("apple", "google"; "dev" only on test servers).</summary>
        public string[] Providers { get; private set; } = new string[0];
        /// <summary>Raised when a Google / Apple sign-in ends: (message, error). The account screen shows it.</summary>
        public event Action<string, string> ExternalFinished;
        public bool ExternalPending { get; private set; }
        /// <summary>Goes up whenever this device switches account (sign in, sign out, deletion): screens drop what they cached.</summary>
        public int AccountGeneration { get; private set; }

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
            // Android brings Google / Apple sign-ins back as orsuun://auth deep links (a cold start carries it in absoluteURL).
            Application.deepLinkActivated += OnAuthCallback;
            if (!string.IsNullOrEmpty(Application.absoluteURL)) OnAuthCallback(Application.absoluteURL);
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
        /// <summary>
        /// A store purchase to credit (StoreFront): completes with (message, error, final). final is true when the server
        /// has answered for good (credited, already credited, or refused as not a real purchase): only then may the phone
        /// finish the purchase with the store. A missing store setup, a store outage or no answer keep it pending, so the
        /// store hands it over again later.
        /// </summary>
        public IEnumerator AmberPurchase(string store, string productId, string receipt, Action<string, string, bool> done)
        {
            if (_session == null) { done(null, "Not connected to the server.", false); yield break; }
            using var req = new UnityWebRequest(_baseUrl + "/v1/caravan/purchase", "POST");
            string body = JsonUtility.ToJson(new AmberPurchaseRequest { store = store, productId = productId, receipt = receipt });
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.SetRequestHeader("Content-Type", "application/json");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("X-Session", _session);
            req.timeout = 20;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var answer = JsonUtility.FromJson<AmberPurchaseDto>(req.downloadHandler.text);
                if (answer.state != null) Apply(answer.state);
                done(answer.message, null, true);
                yield break;
            }
            ErrorDto error = req.responseCode >= 400 && req.downloadHandler.text.Length > 0 ? JsonUtility.FromJson<ErrorDto>(req.downloadHandler.text) : null;
            bool final = error != null && (error.code == "bad_receipt" || error.code == "no_pack" || error.code == "bad_store");
            done(null, error?.message ?? "No answer from the server.", final);
        }

        private readonly HashSet<string> _milestones = new HashSet<string>();

        /// <summary>A first only the phone sees ("tutorial-3", "tutorial-done", "tutorial-skipped") for the team's funnel;
        /// sent once a run, answer ignored.</summary>
        public void Milestone(string name)
        {
            if (!Online || !_milestones.Add(name)) return;
            StartCoroutine(Send("POST", "/v1/milestone", JsonUtility.ToJson(new MilestoneRequest { name = name }), true, _ => { }, _ => { }));
        }

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
            PlayerPrefs.DeleteKey(DeviceTokenKey);
            PlayerPrefs.Save();
            Restart();
            done(null);
        }

        /// <summary>Drops the session and logs in again by the device token (after a sign in, sign out or deletion).</summary>
        private void Restart()
        {
            StopAllCoroutines();
            _session = null;
            Online = false;
            Status = "connecting";
            Email = "";
            EmailVerified = false;
            Logins = new string[0];
            GuildView = null;
            Friends = null;
            FriendAsks = 0;
            GuildInvites = 0;
            MarketView = null;
            War = null;
            Guild = null;
            Banner = Banner.None;
            Lobby = null;
            Depot = null;
            InLobby = false;
            AccountGeneration++;
            StartCoroutine(Run());
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void OrsuunAuth_Start(string url, string scheme);
#endif

        /// <summary>
        /// Sign in with Google / Apple: the server gives the provider's page, which opens in the in-app sign-in sheet
        /// (iOS) or the browser; it ends on an orsuun://auth link with a one-time ticket (OnAuthCallback / deep link).
        /// </summary>
        public IEnumerator BeginExternal(string provider, Action<string> done)
        {
            string failure = null, url = null;
            yield return Post(InLobby ? "/v1/lobby/external/begin" : "/v1/auth/external/begin", JsonUtility.ToJson(new ExternalBeginRequest { provider = provider }), true,
                json => url = JsonUtility.FromJson<ExternalBeginDto>(json).url, error => failure = error ?? "No answer from the server.");
            if (failure == null && !string.IsNullOrEmpty(url))
            {
                ExternalPending = true;
#if UNITY_IOS && !UNITY_EDITOR
                OrsuunAuth_Start(url, "orsuun");
#else
                Application.OpenURL(url);
#endif
            }
            done(failure);
        }

        /// <summary>The iOS sign-in sheet's result (UnitySendMessage), or a deep link from the browser (Android).</summary>
        public void OnAuthCallback(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("orsuun://auth")) return;
            ExternalPending = false;
            string ticket = QueryValue(url, "ticket");
            if (ticket == null)
            {
                string error = QueryValue(url, "error") ?? "failed";
                ExternalFinished?.Invoke(null, error == "cancelled" ? "Sign-in was cancelled." : UnityWebRequest.UnEscapeURL(error));
                return;
            }
            StartCoroutine(RedeemTicket(ticket));
        }

        private static string QueryValue(string url, string key)
        {
            int q = url.IndexOf('?');
            if (q < 0) return null;
            foreach (string pair in url.Substring(q + 1).Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && pair.Substring(0, eq) == key) return UnityWebRequest.UnEscapeURL(pair.Substring(eq + 1));
            }
            return null;
        }

        private IEnumerator RedeemTicket(string ticket)
        {
            string failure = null;
            ExternalLoginResultDto result = null;
            string device = PlayerPrefs.GetString(DeviceTokenKey, "");
            yield return Post("/v1/auth/ticket", JsonUtility.ToJson(new ExternalTicketRequest { ticket = ticket, deviceToken = device }), false,
                json => result = JsonUtility.FromJson<ExternalLoginResultDto>(json), error => failure = error ?? "No answer from the server.");
            if (failure != null)
            {
                ExternalFinished?.Invoke(null, failure);
                yield break;
            }
            string name = result.provider == "apple" ? "Apple" : result.provider == "google" ? "Google" : "the test sign-in";
            string message = result.switched ? $"Signed in with {name}. Welcome back." : $"Your account is now saved with {name}.";
            // This device now points at that hero (switched or not): log in again to load it.
            Restart();
            ExternalFinished?.Invoke(message, null);
        }

        private IEnumerator FetchProviders()
        {
            yield return Send("GET", "/v1/auth/providers", null, false, json =>
            {
                ProvidersDto dto = JsonUtility.FromJson<ProvidersDto>(json);
                if (dto?.providers != null) Providers = dto.providers;
            }, _ => { });
        }

        /// <summary>At the character screen: signed in, no character chosen yet (CharacterPanel shows).</summary>
        public bool InLobby { get; private set; }
        /// <summary>The server answers: a character is played (Online) or the character screen has loaded.</summary>
        public bool Connected => Online || InLobby && Lobby != null;
        /// <summary>Signing in or at the character screen: no hero is played yet (the game stays covered).</summary>
        public bool WaitingForHero => !Online && (InLobby || Status == "connecting");
        /// <summary>The login's characters, Banner and Amber, from the last lobby call.</summary>
        public LobbyDto Lobby { get; private set; }
        private bool _chosen;

        public IEnumerator FetchLobby(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/lobby", null, true, json => ApplyLobby(JsonUtility.FromJson<LobbyDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>The hero (character) this device plays, once the server has said.</summary>
        public string AccountId { get; private set; } = "";

        /// <summary>The login (the player's account) this device plays: store purchases are tagged with it.</summary>
        public string LoginId { get; private set; } = "";

        private void ApplyLobby(LobbyDto lobby)
        {
            Lobby = lobby;
            if (!string.IsNullOrEmpty(lobby.loginId)) LoginId = lobby.loginId;
            Email = lobby.email ?? "";
            EmailVerified = lobby.emailVerified;
            if (lobby.links > 0 && Logins.Length == 0) Logins = new[] { "linked" };
            if (!string.IsNullOrEmpty(lobby.banner) && Enum.TryParse(lobby.banner, out Banner b)) Banner = b;
        }

        /// <summary>A new character in a slot (-1: the first free one). Completes with (message, error).</summary>
        public IEnumerator CreateCharacter(string name, HeroClass cls, int slot, Figure figure, Action<string, string> done)
        {
            string failure = null;
            yield return Post("/v1/lobby/create", JsonUtility.ToJson(new CreateCharacterRequest { name = name, heroClass = cls.ToString(), slot = slot, figure = figure.ToString() }), true,
                json => ApplyLobby(JsonUtility.FromJson<LobbyDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure == null ? Lobby?.message : null, failure);
        }

        /// <summary>Plays a character: its state loads and the game goes on (the heartbeat starts).</summary>
        public IEnumerator SelectCharacter(string id, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/lobby/select", JsonUtility.ToJson(new CharacterRequest { characterId = id, name = "" }), true, json =>
            {
                Apply(JsonUtility.FromJson<StateDto>(json));
                _chosen = true;
            }, error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        public IEnumerator DeleteCharacter(string id, string typedName, Action<string, string> done)
        {
            string failure = null;
            yield return Post("/v1/lobby/delete", JsonUtility.ToJson(new CharacterRequest { characterId = id, name = typedName }), true,
                json => ApplyLobby(JsonUtility.FromJson<LobbyDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure == null ? Lobby?.message : null, failure);
        }

        /// <summary>Back to the character screen (MENU's CHARACTERS): signs in again and waits there.</summary>
        public void ChangeCharacter() => Restart();

        /// <summary>The shared depot (Rules.Characters.DepotSlots pieces), from the last depot call.</summary>
        public DepotDto Depot { get; private set; }

        public IEnumerator FetchDepot(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/depot", null, true, json =>
            {
                Depot = JsonUtility.FromJson<DepotDto>(json);
                Apply(Depot.state);
            }, error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>A bag piece into the depot (put) or a depot piece into the bag. Completes with (message, error).</summary>
        public IEnumerator DepotMove(bool put, string itemId, Action<string, string> done)
        {
            string failure = null;
            yield return Post(put ? "/v1/depot/put" : "/v1/depot/take", JsonUtility.ToJson(new DepotRequest { requestId = NewRequestId(), itemId = itemId }), true, json =>
            {
                Depot = JsonUtility.FromJson<DepotDto>(json);
                Apply(Depot.state);
            }, error => failure = error ?? "No answer from the server.");
            done(failure == null ? Depot?.message : null, failure);
        }

        /// <summary>Sign up: saves an email and password to the account (from the character screen or the game).</summary>
        public IEnumerator Register(string email, string password, Action<string> done)
        {
            string failure = null;
            string body = JsonUtility.ToJson(new RegisterRequest { email = email, password = password });
            if (InLobby)
                yield return Post("/v1/lobby/register", body, true, json => ApplyLobby(JsonUtility.FromJson<LobbyDto>(json)), error => failure = error ?? "No answer from the server.");
            else
                yield return Post("/v1/auth/register", body, true, json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>Sign in: points this device at the account, then logs in again (the hero on screen is replaced).</summary>
        public IEnumerator SignIn(string email, string password, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/auth/login", JsonUtility.ToJson(new LoginRequest { email = email, password = password, deviceToken = SavedDeviceToken() }), false,
                _ => { }, error => failure = error ?? "No answer from the server.");
            if (failure == null) Restart();
            done(failure);
        }

        private static string SavedDeviceToken()
        {
            string token = PlayerPrefs.GetString(DeviceTokenKey, "");
            if (token.Length == 0)
            {
                token = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(DeviceTokenKey, token);
                PlayerPrefs.Save();
            }
            return token;
        }

        /// <summary>Forgot the password: the server emails a code (it answers the same whether or not the email has an account). Completes with (message, error).</summary>
        public IEnumerator ForgotPassword(string email, Action<string, string> done)
        {
            string failure = null, message = null;
            yield return Post("/v1/auth/forgot", JsonUtility.ToJson(new ForgotRequest { email = email }), false,
                json => message = JsonUtility.FromJson<MessageDto>(json).message, error => failure = error ?? "No answer from the server.");
            done(message, failure);
        }

        /// <summary>A new password with the emailed code: this device then switches to the account, like SIGN IN.</summary>
        public IEnumerator ResetPassword(string email, string code, string password, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/auth/reset", JsonUtility.ToJson(new ResetRequest { email = email, code = code, password = password, deviceToken = SavedDeviceToken() }), false,
                _ => { }, error => failure = error ?? "No answer from the server.");
            if (failure == null) Restart();
            done(failure);
        }

        /// <summary>Emails a code that proves the account's email (code null), or checks one. Completes with (message, error).</summary>
        public IEnumerator VerifyEmail(string code, Action<string, string> done)
        {
            string failure = null, message = null;
            string path = (InLobby ? "/v1/lobby" : "/v1/auth") + (code == null ? "/verify/send" : "/verify");
            yield return Post(path, code == null ? "{}" : JsonUtility.ToJson(new VerifyRequest { code = code }), true, json =>
            {
                var answer = JsonUtility.FromJson<MessageDto>(json);
                message = answer.message;
                EmailVerified = answer.emailVerified;
            }, error => failure = error ?? "No answer from the server.");
            done(message, failure);
        }

        /// <summary>Sign out: ends this device's session and starts a new guest with a new device token.</summary>
        public IEnumerator SignOut(Action<string> done)
        {
            string failure = null;
            if (_session != null) yield return Post(InLobby ? "/v1/lobby/signout" : "/v1/auth/signout", "{}", true, _ => { }, error => failure = error);
            PlayerPrefs.DeleteKey(DeviceTokenKey);
            PlayerPrefs.Save();
            Restart();
            done(failure);
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

            yield return Post("/v1/auth/guest", JsonUtility.ToJson(new GuestLoginRequest { deviceToken = token, lobby = true }), false,
                json =>
                {
                    var r = JsonUtility.FromJson<GuestLoginResponse>(json);
                    _session = r.sessionToken;
                    if (!string.IsNullOrEmpty(r.loginId)) LoginId = r.loginId;
                },
                error => Status = "LOCAL MODE: " + error);

            if (_session == null) yield break;
            StartCoroutine(FetchProviders());

            // The character screen (25 Sep 2026): the device waits there until a character is chosen (CharacterPanel).
            _chosen = false;
            InLobby = true;
            Status = "choose a hero";
            yield return FetchLobby(_ => { });
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autoselect") >= 0 && Lobby?.characters != null && Lobby.characters.Length > 0)
            {
                // Screenshots: straight on with the character played last.
                CharacterSlotDto last = Lobby.characters[0];
                foreach (CharacterSlotDto c in Lobby.characters)
                    if (string.CompareOrdinal(c.lastPlayedUtc, last.lastPlayedUtc) > 0) last = c;
                yield return SelectCharacter(last.id, _ => { });
            }
            while (!_chosen) yield return null;
            InLobby = false;

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
                if (!sent && Status.Contains("Choose a character")) { Restart(); yield break; }   // deleted from another device
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
        public IEnumerator Forge(ForgeMethod method, EquipSlot slot, string itemId, Action<ForgeResultDto, string> done, bool pearl = false)
        {
            var req = new ForgeRequest { requestId = Guid.NewGuid().ToString("N"), method = method.ToString(), slot = slot.ToString(), itemId = itemId, pearl = pearl };
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

        /// <summary>Sells the picked bag pieces to the merchant in one request (all or nothing). Completes with an error, or null.</summary>
        public IEnumerator SellPieces(string[] itemIds, Action<string> done)
        {
            string failure = null;
            var request = new BagSellRequest { requestId = Guid.NewGuid().ToString("N"), itemId = Guid.Empty.ToString(), itemIds = itemIds };
            yield return Post("/v1/bag/sell", JsonUtility.ToJson(request), true, json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>Sells a bag piece to the merchant for sorn (Rules.Bag.SellPrice). Completes with an error, or null.</summary>
        public IEnumerator SellPiece(string itemId, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/bag/sell", JsonUtility.ToJson(new EquipRequest { requestId = Guid.NewGuid().ToString("N"), itemId = itemId }), true,
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

        public IEnumerator ClaimBounty(int bountyId, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/bounty/claim", JsonUtility.ToJson(new ClaimBountyRequest { requestId = Guid.NewGuid().ToString("N"), bountyId = bountyId }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        public IEnumerator Buy(int shopItemId, int count, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/shop/buy", JsonUtility.ToJson(new ShopBuyRequest { requestId = Guid.NewGuid().ToString("N"), shopItemId = shopItemId, count = count }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>An Etching Needle on an owned piece. Completes with (result, error).</summary>
        public IEnumerator Etch(string itemId, Action<EtchResultDto, string> done)
        {
            EtchResultDto result = null;
            string failure = null;
            yield return Post("/v1/etch", JsonUtility.ToJson(new EtchRequest { requestId = Guid.NewGuid().ToString("N"), itemId = itemId }), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                Apply(state);
                result = state.lastEtch;
            }, error => failure = error);
            done(result, failure);
        }

        public IEnumerator Pin(string itemId, int index, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/pin", JsonUtility.ToJson(new PinRequest { requestId = Guid.NewGuid().ToString("N"), itemId = itemId, index = index }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>Changes the account's Banner (once a season, Rules.Banners.ChangeOathstones from this hero).</summary>
        public IEnumerator ChangeBanner(Banner banner, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/banner/change", JsonUtility.ToJson(new BannerChangeRequest { requestId = NewRequestId(), banner = banner.ToString() }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>Reads toward each skill's next Mastered step, and seconds of rest left (at SkillReadyAt), by book id.</summary>
        public int[] SkillProgress { get; private set; } = new int[Rules.Books.Count];
        public long[] SkillReadySeconds { get; private set; } = new long[Rules.Books.Count];
        public float SkillReadyAt { get; private set; }
        /// <summary>Honor (Rules.SkillGrades): spent on Oathstone tries.</summary>
        public long Honor { get; private set; }

        /// <summary>Seconds a skill (by book id) must still rest before its next read.</summary>
        public long SkillRestLeft(int book) =>
            book < SkillReadySeconds.Length ? Math.Max(0, SkillReadySeconds[book] - (long)(Time.realtimeSinceStartup - SkillReadyAt)) : 0;

        /// <summary>One try at a skill's next grade (slot of the class played). Completes with (result, error).</summary>
        public IEnumerator TrainSkill(int slot, Action<SkillTrainDto, string> done)
        {
            string failure = null;
            SkillTrainDto result = null;
            yield return Post("/v1/skills/train", JsonUtility.ToJson(new SkillTrainRequest { requestId = NewRequestId(), slot = slot }), true, json =>
            {
                result = JsonUtility.FromJson<SkillTrainDto>(json);
                Apply(result.state);
            }, error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        /// <summary>Oath Renewal (Rules.OathRenewal): back to level 1 with a lasting bonus.</summary>
        public IEnumerator Renew(Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/renew", JsonUtility.ToJson(new RenewRequest { requestId = NewRequestId() }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>The oath to a Banner (once, for the whole account): at the character screen, or in the game for older accounts.</summary>
        public IEnumerator Swear(Banner banner, Action<string> done)
        {
            string failure = null;
            string body = JsonUtility.ToJson(new BannerRequest { banner = banner.ToString() });
            if (InLobby)
                yield return Post("/v1/lobby/banner", body, true, json => ApplyLobby(JsonUtility.FromJson<LobbyDto>(json)), error => failure = error ?? "No answer from the server.");
            else
                yield return Post("/v1/banner", body, true, json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>Refreshes War (standings, fortresses, cooldown).</summary>
        public IEnumerator FetchWar(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/war", null, true, json =>
            {
                War = JsonUtility.FromJson<WarDto>(json);
                WarReceivedAt = Time.realtimeSinceStartup;
            }, error => failure = error);
            done(failure);
        }

        /// <summary>One siege fight; the result carries the seed the client replays.</summary>
        public IEnumerator Siege(int fortressId, Action<SiegeResultDto, string> done)
        {
            SiegeResultDto result = null;
            string failure = null;
            yield return Post("/v1/siege", JsonUtility.ToJson(new SiegeRequest { requestId = Guid.NewGuid().ToString("N"), fortressId = fortressId }), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                result = state.lastSiege;
                Apply(state);
            }, error => failure = error);
            done(result, failure);
        }

        /// <summary>A keep fight at the Sunday siege (a contender storms, a holder holds); replayed like a siege fight.</summary>
        public IEnumerator KeepFight(int fortressId, Action<SiegeResultDto, string> done)
        {
            SiegeResultDto result = null;
            string failure = null;
            yield return Post("/v1/keep/fight", JsonUtility.ToJson(new KeepFightRequest { requestId = NewRequestId(), fortressId = fortressId }), true, json =>
            {
                StateDto state = JsonUtility.FromJson<StateDto>(json);
                result = state.lastSiege;
                Apply(state);
            }, error => failure = error);
            done(result, failure);
        }

        /// <summary>Adds treasury sorn to the guild's bid on a keep; completes with (message, error) and refreshes War.</summary>
        public IEnumerator KeepBid(int fortressId, long amount, Action<string, string> done)
        {
            string failure = null;
            yield return Post("/v1/keep/bid", JsonUtility.ToJson(new KeepBidRequest { requestId = NewRequestId(), fortressId = fortressId, amount = amount }), true, json =>
            {
                War = JsonUtility.FromJson<WarDto>(json);
                WarReceivedAt = Time.realtimeSinceStartup;
            }, error => failure = error);
            done(failure == null ? War?.message : null, failure);
        }

        /// <summary>The Campaign Trail from the last state (Rules.CampaignTrail); null until a server that has one answers.</summary>
        public TrailDto Trail { get; private set; }
        /// <summary>The login calendar (Rules.DailyLogin), from the last state; null before one came or from older servers.</summary>
        public DailyDto Daily { get; private set; }
        private float _trailAt;
        public long TrailSecondsLeft => Trail == null ? 0 : Math.Max(0, Trail.secondsLeft - (long)(Time.realtimeSinceStartup - _trailAt));

        /// <summary>Amber and the wardrobe from the last state (Rules.Wardrobe); SecondsLeft counts down from its arrival.</summary>
        public WardrobeDto Wardrobe { get; private set; }
        private float _wardrobeAt;
        private float _wardrobeCheck;
        public long Amber => Wardrobe?.amber ?? 0;

        /// <summary>Seconds a held piece has left now (0: not held or run out).</summary>
        public long SecondsLeft(string pieceId)
        {
            if (Wardrobe?.pieces == null || string.IsNullOrEmpty(pieceId)) return 0;
            foreach (WardrobePieceDto p in Wardrobe.pieces)
                if (p.id == pieceId) return Math.Max(0, p.secondsLeft - (long)(Time.realtimeSinceStartup - _wardrobeAt));
            return 0;
        }

        /// <summary>The id worn in a slot ("" for none), whether or not its time has run out.</summary>
        public string WornId(WardrobeKind kind) =>
            Wardrobe == null ? "" : kind == WardrobeKind.Skin ? Wardrobe.skin : kind == WardrobeKind.Mount ? Wardrobe.mount : Wardrobe.companion;

        /// <summary>The worn pieces with time left, as the server counts them in the hero.</summary>
        public List<WardrobeDef> WornPieces()
        {
            var list = new List<WardrobeDef>();
            foreach (WardrobeKind kind in new[] { WardrobeKind.Skin, WardrobeKind.Mount, WardrobeKind.Companion })
            {
                WardrobeDef def = Rules.Wardrobe.Find(WornId(kind));
                if (def != null && SecondsLeft(def.Id) > 0) list.Add(def);
            }
            return list;
        }

        private void Update()
        {
            // A worn piece whose time runs out stops counting here as it does on the server.
            if (Wardrobe == null || Time.realtimeSinceStartup < _wardrobeCheck) return;
            _wardrobeCheck = Time.realtimeSinceStartup + 1f;
            _player.SetWorn(WornPieces());
        }

        public IEnumerator CaravanBuy(string pieceId, int days, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/caravan/buy", JsonUtility.ToJson(new CaravanBuyRequest { requestId = NewRequestId(), pieceId = pieceId, days = days }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>Wears a held piece, or with an empty id takes off what is worn in <paramref name="kind"/>.</summary>
        public IEnumerator Wear(string pieceId, WardrobeKind kind, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/wardrobe/wear", JsonUtility.ToJson(new WearRequest { requestId = NewRequestId(), pieceId = pieceId ?? "", kind = kind.ToString() }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>A live direct trade in brief, from the last /me or heartbeat (an invitation to answer, a window to go back to).</summary>
        public TradeBriefDto TradeBrief { get; private set; }
        /// <summary>The trade window as the server last showed it (Rules.DirectTrade); id 0 when there is none.</summary>
        public TradeDto Trade { get; private set; }

        private void ApplyTrade(string json)
        {
            Trade = JsonUtility.FromJson<TradeDto>(json);
            TradeBrief = Trade.id > 0 && (Trade.state == "Invited" || Trade.state == "Open")
                ? new TradeBriefDto { id = Trade.id, state = Trade.state, incoming = Trade.incoming, otherName = Trade.otherName } : null;
            if (Trade.hero != null && Trade.hero.inventory != null && !string.IsNullOrEmpty(Trade.hero.accountId)) Apply(Trade.hero);
        }

        public IEnumerator FetchTrade(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/trade", null, true, ApplyTrade, error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>A trade call (invite, accept, cancel, offer, press): the window comes back, or the refusal's message.</summary>
        private IEnumerator TradeCall(string path, string body, Action<string> done)
        {
            string failure = null;
            yield return Post(path, body, true, ApplyTrade, error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>Asks a hero to trade by name, or by id (<paramref name="accountId"/>, from chat or the friend list).</summary>
        public IEnumerator TradeInvite(string name, Action<string> done, string accountId = null) =>
            TradeCall("/v1/trade/invite", JsonUtility.ToJson(new TradeInviteRequest { requestId = NewRequestId(), name = name ?? "", accountId = accountId ?? NoId }), done);

        /// <summary>The empty id: a request that names a hero instead.</summary>
        public const string NoId = "00000000-0000-0000-0000-000000000000";

        /// <summary>Friend requests waiting for this hero, and guild invites (from the last /me or heartbeat, or friend and guild calls).</summary>
        public int FriendAsks { get; private set; }
        public int GuildInvites { get; private set; }
        /// <summary>The friend list as the server last showed it (Rules.Friends).</summary>
        public FriendsDto Friends { get; private set; }

        private void ApplyFriends(string json)
        {
            Friends = JsonUtility.FromJson<FriendsDto>(json);
            FriendAsks = Friends.asking?.Length ?? 0;
        }

        // ---- The mailbox (Rules.Mail): letters from the Exchange and the Pits, with what they hold until taken.

        /// <summary>Unread letters (from the last /me or heartbeat; opening the mailbox reads them all).</summary>
        public int MailUnread { get; private set; }
        /// <summary>Fishing (Rules.Fishing): at Old Nergui's river (no hunting there), the meal and the Tireless Rod, and the
        /// rod's catches not yet shown (the river screen takes them).</summary>
        public RiverDto River { get; private set; }
        public float RiverAt { get; private set; }
        public bool AtRiver => Online && River != null && River.atRiver;
        public int AutoCatches { get; set; }
        /// <summary>A fish's boost seconds left (each fish runs on its own clock, beside the others).</summary>
        public long MealSecondsLeft(int fish) => River?.mealSeconds == null || fish < 0 || fish >= River.mealSeconds.Length ? 0
            : Math.Max(0, River.mealSeconds[fish] - (long)(Time.realtimeSinceStartup - RiverAt));
        public long RodSecondsLeft => River == null ? 0 : Math.Max(0, River.rodSecondsLeft - (long)(Time.realtimeSinceStartup - RiverAt));
        /// <summary>The mailbox as the server last showed it.</summary>
        public MailDto Mail { get; private set; }

        // ---- The guild raid (Rules.GuildRaids).

        public GuildRaidDto Raid { get; private set; }
        /// <summary>When the raid view came (its seconds left count down from here).</summary>
        public float RaidAt { get; private set; }

        public IEnumerator FetchRaid(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/guild/raid", null, true, json =>
            {
                Raid = JsonUtility.FromJson<GuildRaidDto>(json);
                RaidAt = Time.realtimeSinceStartup;
            }, error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>One raid fight: the server rolls it; the lane replays its seed.</summary>
        public IEnumerator FightRaid(Action<GuildRaidFightDto, string> done)
        {
            GuildRaidFightDto result = null;
            string failure = null;
            yield return Post("/v1/guild/raid/fight", JsonUtility.ToJson(new RaidFightRequest { requestId = NewRequestId() }), true, json =>
            {
                result = JsonUtility.FromJson<GuildRaidFightDto>(json);
                Raid = result.raid;
                RaidAt = Time.realtimeSinceStartup;
                if (result.state != null && result.state.inventory != null && result.state.items != null) Apply(result.state);
            }, error => failure = error);
            done(result, failure);
        }

        // ---- Titles and achievements (Rules.Achievements).

        /// <summary>Achievements done and waiting to be claimed (the MENU badge), and the title worn.</summary>
        public int AchievementsReady { get; private set; }
        /// <summary>The hero's Commander fights, dungeon clears, bounties claimed and Pit wins, for the goal line.</summary>
        public GoalCountsDto GoalCounts { get; private set; } = new GoalCountsDto();
        public string Title { get; private set; } = "";
        public AchievementsDto Achievements { get; private set; }

        public IEnumerator FetchAchievements(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/achievements", null, true, ApplyAchievements, error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        public IEnumerator ClaimAchievement(int id, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/achievements/claim", JsonUtility.ToJson(new AchievementClaimRequest { requestId = NewRequestId(), id = id }), true,
                ApplyAchievements, error => failure = error);
            done(failure);
        }

        /// <summary>Wears a claimed achievement's title (0: none).</summary>
        public IEnumerator WearTitle(int id, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/achievements/title", JsonUtility.ToJson(new TitleRequest { id = id }), true, ApplyAchievements, error => failure = error);
            done(failure);
        }

        private void ApplyAchievements(string json)
        {
            AchievementsDto view = JsonUtility.FromJson<AchievementsDto>(json);
            Achievements = view;
            Title = view.title ?? "";
            if (view.state != null && view.state.inventory != null && view.state.items != null) Apply(view.state);
            int ready = 0;
            if (view.list != null)
                foreach (AchievementDto a in view.list)
                    if (a.done && !a.claimed) ready++;
            AchievementsReady = ready;
        }

        public IEnumerator FetchMail(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/mail", null, true, ApplyMail, error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>Takes what a letter holds (letterId 0: every letter's). Completes with an error, or null.</summary>
        public IEnumerator TakeMail(long letterId, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/mail/take", JsonUtility.ToJson(new MailTakeRequest { requestId = NewRequestId(), letterId = letterId }), true,
                ApplyMail, error => failure = error);
            done(failure);
        }

        /// <summary>Throws away a letter with nothing left in it (letterId 0: every such letter).</summary>
        public IEnumerator DeleteMail(long letterId, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/mail/delete", JsonUtility.ToJson(new MailDeleteRequest { letterId = letterId }), true,
                ApplyMail, error => failure = error);
            done(failure);
        }

        /// <summary>Screenshots in local play (-mail): a mailbox of made-up letters.</summary>
        public void SampleMail()
        {
            string now = DateTime.UtcNow.ToString("o"), before = DateTime.UtcNow.AddDays(-2).ToString("o");
            var boots = new ItemDto { id = "sample", slot = "Shoes", name = "Rare Felt Boots", itemLevel = 24, rarity = "Rare", upgradeLevel = 3,
                lockedEtchingIndex = -1, etchings = new EtchingDto[0], sockets = new SocketDto[0] };
            Mail = new MailDto
            {
                message = "",
                letters = new[]
                {
                    new LetterDto { id = 4, kind = "sale", from = "The Salt Exchange", title = "Sold: Epic Tamga Sword +7", utc = now, sorn = 1_425_000,
                        body = "Talon Varga bought Epic Tamga Sword +7 for 1,500,000 sorn. The Exchange keeps its 5% (75,000); 1,425,000 sorn is in this letter for you." },
                    new LetterDto { id = 3, kind = "sale", from = "The Salt Exchange", title = "Sold: 5 × Turnstone", utc = now, sorn = 4_750,
                        body = "Ash Keller bought 5 × Turnstone for 5,000 sorn. The Exchange keeps its 5% (250); 4,750 sorn is in this letter for you." },
                    new LetterDto { id = 2, kind = "returned", from = "The Salt Exchange", title = "Not sold: Rare Felt Boots +3", utc = before, read = true, item = boots,
                        body = "Nobody bought Rare Felt Boots +3 at 90,000 sorn in 48 hours, so it comes back to you with this letter." },
                    new LetterDto { id = 1, kind = "pits", from = "The Pits", title = "Pit season 2026-38: rank 4", utc = before, read = true, taken = true,
                        body = "The season is over. You finished 4 with a rating of 1,412 and the Pits paid you 110 Laurels. Spend them at the Pit shop." },
                },
            };
            MailUnread = 2;
        }

        private void ApplyMail(string json)
        {
            MailDto view = JsonUtility.FromJson<MailDto>(json);
            Mail = view;
            MailUnread = view.unread;
            if (view.state != null && view.state.inventory != null && view.state.items != null) Apply(view.state);
        }

        // ---- Private messages (Rules.Whispers): kept with no expiry, on the MESSAGES screen.

        /// <summary>Unread messages for this hero (from the last /me or heartbeat, and the message calls).</summary>
        public int WhisperUnread { get; private set; }
        /// <summary>The conversation list as the server last showed it.</summary>
        public WhispersDto Whispers { get; private set; }

        public IEnumerator FetchWhispers(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/whispers", null, true, json =>
            {
                Whispers = JsonUtility.FromJson<WhispersDto>(json);
                WhisperUnread = Whispers.unread;
            }, error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>
        /// One conversation: the newest page (after 0, before 0), new lines after the last id held, or the page before an
        /// id. Completes with (thread, error); opening it marks what they sent as read.
        /// </summary>
        public IEnumerator FetchWhisperThread(string accountId, long after, long before, Action<WhisperThreadDto, string> done, string name = null)
        {
            string failure = null;
            WhisperThreadDto thread = null;
            string who = !string.IsNullOrEmpty(accountId) ? "id=" + accountId : "name=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(name ?? "");
            string path = $"/v1/whispers/thread?{who}" + (after > 0 ? $"&after={after}" : "") + (before > 0 ? $"&before={before}" : "");
            yield return Send("GET", path, null, true, json => thread = JsonUtility.FromJson<WhisperThreadDto>(json), error => failure = error ?? "No answer from the server.");
            done(thread, failure);
        }

        /// <summary>Sends a message to a hero by id, or by name with NoId; returns the conversation's lines after <paramref name="after"/>.</summary>
        public IEnumerator SendWhisper(string accountId, string name, string text, long after, Action<WhisperThreadDto, string> done)
        {
            string failure = null;
            WhisperThreadDto thread = null;
            string body = JsonUtility.ToJson(new WhisperSendRequest { accountId = accountId ?? NoId, name = name ?? "", text = text, after = after });
            yield return Post("/v1/whispers/send", body, true, json => thread = JsonUtility.FromJson<WhisperThreadDto>(json), error => failure = error ?? "No answer from the server.");
            done(thread, failure);
        }

        public IEnumerator ReportWhisper(long messageId, Action<WhisperThreadDto, string> done)
        {
            string failure = null;
            WhisperThreadDto thread = null;
            yield return Post("/v1/whispers/report", JsonUtility.ToJson(new WhisperReportRequest { messageId = messageId }), true,
                json => thread = JsonUtility.FromJson<WhisperThreadDto>(json), error => failure = error ?? "No answer from the server.");
            done(thread, failure);
        }

        /// <summary>The HUD's count drops as soon as a conversation is opened (the server marked it read).</summary>
        public void WhispersRead(int count) => WhisperUnread = Math.Max(0, WhisperUnread - count);

        public IEnumerator FetchFriends(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/friends", null, true, ApplyFriends, error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>A friend call (add, answer, remove); completes with (message, error).</summary>
        private IEnumerator FriendCall(string path, string body, Action<string, string> done)
        {
            string failure = null;
            yield return Post("/v1/friends/" + path, body, true, ApplyFriends, error => failure = error ?? "No answer from the server.");
            done(failure == null ? Friends?.message : null, failure);
        }

        /// <summary>Asks a hero to be friends, by id (chat) or by name.</summary>
        public IEnumerator AddFriend(string accountId, string name, Action<string, string> done) =>
            FriendCall("add", JsonUtility.ToJson(new FriendAddRequest { accountId = string.IsNullOrEmpty(accountId) ? NoId : accountId, name = name ?? "" }), done);

        public IEnumerator AnswerFriend(string accountId, bool accept, Action<string, string> done) =>
            FriendCall("answer", JsonUtility.ToJson(new FriendAnswerRequest { accountId = accountId, accept = accept }), done);

        public IEnumerator RemoveFriend(string accountId, Action<string, string> done) =>
            FriendCall("remove", JsonUtility.ToJson(new FriendRemoveRequest { accountId = accountId }), done);

        /// <summary>The leader or an officer invites a hero to the guild, by id (chat, friends) or by name.</summary>
        public IEnumerator InviteToGuild(string accountId, string name, Action<string, string> done) =>
            GuildCall("invite", new GuildInviteRequest { requestId = NewRequestId(), accountId = string.IsNullOrEmpty(accountId) ? NoId : accountId, name = name ?? "" }, done);

        public IEnumerator TradeAct(string action, Action<string> done) =>
            TradeCall("/v1/trade/" + action, JsonUtility.ToJson(new TradeRequest { requestId = NewRequestId(), tradeId = Trade?.id ?? TradeBrief?.id ?? 0 }), done);

        /// <summary>Puts the whole offer on the table: pieces, sorn and Technique Scroll stacks (bookId, count).</summary>
        public IEnumerator TradeOffer(string[] itemIds, long sorn, Action<string> done, BookOfferDto[] books = null) =>
            TradeCall("/v1/trade/offer", JsonUtility.ToJson(new TradeOfferRequest { requestId = NewRequestId(), tradeId = Trade?.id ?? 0, itemIds = itemIds, sorn = sorn,
                books = books ?? new BookOfferDto[0] }), done);

        /// <summary>The hero's state again (after a trade the other side finished).</summary>
        public IEnumerator RefreshState()
        {
            yield return Send("GET", "/v1/me", null, true, json => Apply(JsonUtility.FromJson<StateDto>(json)), _ => { });
        }

        /// <summary>Claims tier <paramref name="tier"/>'s ready rewards on both tracks (0: every reward ready, and last season's).</summary>
        public IEnumerator TrailClaim(int tier, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/trail/claim", JsonUtility.ToJson(new TrailClaimRequest { requestId = NewRequestId(), tier = tier }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>Buys this hero the Trail's paid track, or Trail Plus (ten tiers more), with the account's Amber.</summary>
        /// <summary>Takes today's gift from the login calendar. Completes with an error, or null.</summary>
        public IEnumerator ClaimDaily(Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/daily/claim", JsonUtility.ToJson(new DailyClaimRequest { requestId = NewRequestId() }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        public IEnumerator TrailBuy(bool plus, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/trail/buy", JsonUtility.ToJson(new TrailBuyRequest { requestId = NewRequestId(), plus = plus }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>An Amber pack (free on the playtest server until the stores are connected).</summary>
        public IEnumerator AmberPack(int packId, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/caravan/amber", JsonUtility.ToJson(new AmberPackRequest { requestId = NewRequestId(), packId = packId }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error);
            done(failure);
        }

        /// <summary>The Pits (the PITS screen): record, challengers, board; from the last Pits call.</summary>
        public PitsDto Pits { get; private set; }

        public IEnumerator FetchPits(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/pits", null, true, json => Pits = JsonUtility.FromJson<PitsDto>(json), error => failure = error);
            done(failure);
        }

        /// <summary>A Pits call under /v1/pits/ (refresh, shop); completes with (message, error).</summary>
        public IEnumerator PitCall(string path, object request, Action<string, string> done)
        {
            string failure = null;
            yield return Post("/v1/pits/" + path, request == null ? "{}" : JsonUtility.ToJson(request), true,
                json => Pits = JsonUtility.FromJson<PitsDto>(json), error => failure = error);
            done(failure == null ? Pits?.message : null, failure);
        }

        /// <summary>One Pit fight against a challenger on offer; the result carries what the client replays.</summary>
        public IEnumerator PitFight(string opponentId, Action<PitFightDto, string> done)
        {
            PitFightDto result = null;
            string failure = null;
            yield return Post("/v1/pits/fight", JsonUtility.ToJson(new PitFightRequest { requestId = NewRequestId(), opponentId = opponentId }), true, json =>
            {
                result = JsonUtility.FromJson<PitFightDto>(json);
                Apply(result.state);
                Pits = result.pits;
            }, error => failure = error);
            done(result, failure);
        }

        /// <summary>Free dungeon runs left today, and the run waiting at the Chained Smith (0: none), from every state.</summary>
        public int DungeonRunsLeft { get; private set; }
        public long DungeonRunAtSmith { get; private set; }
        /// <summary>The dungeon of the run waiting at its pause floor (the Chained Smith or a rune lock), 0 when none.</summary>
        public int DungeonPausedId { get; private set; }

        /// <summary>Enters a dungeon: the floors up to the smith (or a fall) come back to be replayed.</summary>
        public IEnumerator DungeonEnter(int dungeonId, Action<DungeonResultDto, string> done)
        {
            DungeonResultDto result = null;
            string failure = null;
            yield return Post("/v1/dungeon/enter", JsonUtility.ToJson(new DungeonEnterRequest { requestId = NewRequestId(), dungeonId = dungeonId }), true, json =>
            {
                result = JsonUtility.FromJson<DungeonResultDto>(json);
                Apply(result.state);
            }, error => failure = error);
            done(result, failure);
        }

        /// <summary>Answers the Chained Smith (itemId "" walks on); the rest of the run comes back to be replayed.</summary>
        public IEnumerator DungeonSmith(long runId, string itemId, Action<DungeonResultDto, string> done, string rune = null)
        {
            DungeonResultDto result = null;
            string failure = null;
            yield return Post("/v1/dungeon/smith", JsonUtility.ToJson(new DungeonSmithRequest { requestId = NewRequestId(), runId = runId, itemId = itemId ?? "", rune = rune ?? "" }), true, json =>
            {
                result = JsonUtility.FromJson<DungeonResultDto>(json);
                Apply(result.state);
            }, error => failure = error);
            done(result, failure);
        }

        /// <summary>The guild war view (the GUILD WAR screen), from the last war call.</summary>
        public GuildWarDto GuildWar { get; private set; }
        public float GuildWarReceivedAt { get; private set; }

        public IEnumerator FetchGuildWar(Action<string> done)
        {
            string failure = null;
            yield return Send("GET", "/v1/guild/war", null, true, ApplyGuildWar, error => failure = error);
            done(failure);
        }

        /// <summary>A guild war call under /v1/guild/war/ (signup, flag); completes with (message, error).</summary>
        public IEnumerator GuildWarCall(string path, object request, Action<string, string> done)
        {
            string failure = null;
            yield return Post("/v1/guild/war/" + path, JsonUtility.ToJson(request), true, ApplyGuildWar, error => failure = error);
            done(failure == null ? GuildWar?.message : null, failure);
        }

        /// <summary>One guild war duel on a lane; the result carries what the client replays.</summary>
        public IEnumerator GuildWarFight(int lane, Action<DuelResultDto, string> done)
        {
            DuelResultDto result = null;
            string failure = null;
            yield return Post("/v1/guild/war/fight", JsonUtility.ToJson(new GuildWarFightRequest { requestId = NewRequestId(), lane = lane }), true, json =>
            {
                GuildWarFightDto fight = JsonUtility.FromJson<GuildWarFightDto>(json);
                Apply(fight.state);
                GuildWar = fight.war;
                GuildWarReceivedAt = Time.realtimeSinceStartup;
                result = fight.duel;
            }, error => failure = error);
            done(result, failure);
        }

        private void ApplyGuildWar(string json)
        {
            GuildWar = JsonUtility.FromJson<GuildWarDto>(json);
            GuildWarReceivedAt = Time.realtimeSinceStartup;
        }

        /// <summary>Refreshes GuildView: the account's guild, or (none) guilds to join, filtered by search.</summary>
        public IEnumerator FetchGuild(string search, Action<string> done)
        {
            string failure = null;
            string path = "/v1/guild" + (string.IsNullOrEmpty(search) ? "" : "?q=" + UnityWebRequest.EscapeURL(search));
            yield return Send("GET", path, null, true, ApplyGuild, error => failure = error);
            done(failure);
        }

        /// <summary>A guild call (path under /v1/guild/); completes with (message, error).</summary>
        public IEnumerator GuildCall(string path, object request, Action<string, string> done)
        {
            string failure = null;
            yield return Post("/v1/guild/" + path, JsonUtility.ToJson(request), true, ApplyGuild, error => failure = error);
            done(failure == null ? GuildView?.message : null, failure);
        }

        private void ApplyGuild(string json)
        {
            GuildViewDto view = JsonUtility.FromJson<GuildViewDto>(json);
            GuildView = view;
            GuildInvites = view.mine != null && !string.IsNullOrEmpty(view.mine.id) ? 0 : view.invites?.Length ?? 0;
            if (view.state != null && view.state.inventory != null && view.state.items != null) Apply(view.state);
        }

        public static string NewRequestId() => Guid.NewGuid().ToString("N");

        /// <summary>A chat channel's lines after the given id. Completes with (lines, error).</summary>
        public IEnumerator FetchChat(string channel, long after, Action<ChatDto, string> done)
        {
            ChatDto result = null;
            string failure = null;
            yield return Send("GET", $"/v1/chat?channel={channel}&after={after}", null, true, json => result = JsonUtility.FromJson<ChatDto>(json), error => failure = error);
            done(result, failure);
        }

        public IEnumerator Say(string channel, string text, long after, Action<ChatDto, string> done)
        {
            ChatDto result = null;
            string failure = null;
            yield return Post("/v1/chat", JsonUtility.ToJson(new ChatSayRequest { channel = channel, text = text, after = after }), true,
                json => result = JsonUtility.FromJson<ChatDto>(json), error => failure = error);
            done(result, failure);
        }

        /// <summary>A leaderboard ("level", "stage", "pits", "guilds"; period "week" or "all"). Completes with (board, error).</summary>
        public IEnumerator FetchLeaderboard(string board, string period, Action<LeaderboardDto, string> done)
        {
            LeaderboardDto result = null;
            string failure = null;
            yield return Send("GET", $"/v1/leaderboard?board={board}&period={period}", null, true,
                json => result = JsonUtility.FromJson<LeaderboardDto>(json), error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        /// <summary>The Bannerkin (Rules.Bannerkin): what it wears and what that makes of its casts (from the last state).</summary>
        public KinDto Kin { get; private set; }

        /// <summary>A Temper attempt past +9 on a piece (Rules.Tempering). Completes with (result, error).</summary>
        public IEnumerator Temper(string itemId, Action<TemperDto, string> done)
        {
            TemperDto result = null;
            string failure = null;
            yield return Post("/v1/temper", JsonUtility.ToJson(new TemperRequest { requestId = Guid.NewGuid().ToString("N"), itemId = itemId }), true, json =>
            {
                result = JsonUtility.FromJson<TemperDto>(json);
                if (result.state != null) Apply(result.state);
            }, error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        /// <summary>The Bannerkin joins (level 25).</summary>
        public IEnumerator KinJoin(Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/kin/join", "{}", true, json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>Gives the Bannerkin a piece from the bag (what it wore in that slot comes back to the bag).</summary>
        public IEnumerator KinWear(string itemId, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/kin/wear", JsonUtility.ToJson(new KinWearRequest { requestId = Guid.NewGuid().ToString("N"), itemId = itemId }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>Goes to Old Nergui's river (the hunt stops) or back to the hunt.</summary>
        public IEnumerator GoRiver(bool go, Action<string> done)
        {
            string failure = null;
            yield return Post(go ? "/v1/river/go" : "/v1/river/leave", "{}", true, json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>A cast: completes with (the bite's delay and window, error).</summary>
        public IEnumerator Cast(Action<CastBiteDto, string> done)
        {
            CastBiteDto result = null;
            string failure = null;
            yield return Post("/v1/river/cast", "{}", true, json => result = JsonUtility.FromJson<CastBiteDto>(json), error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        public IEnumerator Reel(Action<ReelDto, string> done)
        {
            ReelDto result = null;
            string failure = null;
            yield return Post("/v1/river/reel", "{}", true, json =>
            {
                result = JsonUtility.FromJson<ReelDto>(json);
                if (result.state != null) Apply(result.state);
            }, error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        /// <summary>The catch's end: landed (the bar filled) or not. Completes with (result, error).</summary>
        public IEnumerator Land(bool landed, Action<ReelDto, string> done)
        {
            ReelDto result = null;
            string failure = null;
            yield return Post("/v1/river/land", JsonUtility.ToJson(new LandRequest { landed = landed }), true, json =>
            {
                result = JsonUtility.FromJson<ReelDto>(json);
                if (result.state != null) Apply(result.state);
            }, error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        public IEnumerator Eat(int fish, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/river/eat", JsonUtility.ToJson(new EatRequest { requestId = Guid.NewGuid().ToString("N"), fish = fish }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        public IEnumerator OpenMussels(int count, Action<OpenMusselsDto, string> done)
        {
            OpenMusselsDto result = null;
            string failure = null;
            yield return Post("/v1/river/open", JsonUtility.ToJson(new OpenMusselsRequest { requestId = Guid.NewGuid().ToString("N"), count = count }), true, json =>
            {
                result = JsonUtility.FromJson<OpenMusselsDto>(json);
                if (result.state != null) Apply(result.state);
            }, error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        /// <summary>The Tireless Rod from the Caravan, for Amber.</summary>
        public IEnumerator BuyRod(int days, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/caravan/rod", JsonUtility.ToJson(new AutoRodRequest { requestId = Guid.NewGuid().ToString("N"), days = days }), true,
                json => Apply(JsonUtility.FromJson<StateDto>(json)), error => failure = error ?? "No answer from the server.");
            done(failure);
        }

        /// <summary>INVITE A FRIEND: this hero's code and what it brought in. Completes with (invite, error).</summary>
        public IEnumerator FetchInvite(Action<InviteDto, string> done)
        {
            InviteDto result = null;
            string failure = null;
            yield return Send("GET", "/v1/invite", null, true,
                json => result = JsonUtility.FromJson<InviteDto>(json), error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        /// <summary>Enters a friend's code (before level 10, once). Completes with (invite, error).</summary>
        public IEnumerator EnterInvite(string code, Action<InviteDto, string> done)
        {
            InviteDto result = null;
            string failure = null;
            yield return Post("/v1/invite", JsonUtility.ToJson(new InviteRequest { code = code }), true,
                json => result = JsonUtility.FromJson<InviteDto>(json), error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        /// <summary>Another hero as anyone may see them (INSPECT). Completes with (hero, error).</summary>
        public IEnumerator Inspect(string heroId, Action<InspectDto, string> done)
        {
            InspectDto result = null;
            string failure = null;
            yield return Send("GET", "/v1/hero/" + heroId, null, true, json => result = JsonUtility.FromJson<InspectDto>(json),
                error => failure = error ?? "No answer from the server.");
            done(result, failure);
        }

        /// <summary>Hands the server this phone's push token (PushSender): letters then reach the phone while the game is shut.</summary>
        public IEnumerator PushToken(string platform, string token)
        {
            if (_session == null) yield break;
            yield return Post("/v1/push-token", JsonUtility.ToJson(new PushTokenRequest { platform = platform, token = token }), true, _ => { }, _ => { });
        }

        /// <summary>Reports a hero's name (kind "hero") or the name of that hero's guild ("guild"). Completes with (message, error).</summary>
        public IEnumerator ReportName(string kind, string accountId, Action<string, string> done)
        {
            string failure = null, message = null;
            yield return Post("/v1/report-name", JsonUtility.ToJson(new NameReportRequest { kind = kind, accountId = accountId }), true,
                json => message = JsonUtility.FromJson<MessageDto>(json).message, error => failure = error ?? "No answer from the server.");
            done(message, failure);
        }

        public IEnumerator ReportLine(long messageId, string channel, Action<string> done)
        {
            string failure = null;
            yield return Post("/v1/chat/report", JsonUtility.ToJson(new ChatReportRequest { messageId = messageId, channel = channel }), true, _ => { }, error => failure = error);
            done(failure);
        }

        /// <summary>Blocks or unblocks a player's chat lines; unblocking an empty id clears the list.</summary>
        public IEnumerator Block(string accountId, bool block, Action<ChatDto, string> done)
        {
            ChatDto result = null;
            string failure = null;
            yield return Post("/v1/chat/block", JsonUtility.ToJson(new ChatBlockRequest { accountId = accountId, block = block, channel = "world" }), true,
                json => result = JsonUtility.FromJson<ChatDto>(json), error => failure = error);
            done(result, failure);
        }

        /// <summary>Refreshes MarketView: one page of listings (slot empty = all; sort cheapest, newest or level) and mine.</summary>
        public IEnumerator FetchMarket(string slot, string sort, int page, Action<string> done, bool books = false, bool goods = false)
        {
            string failure = null;
            string path = $"/v1/market?sort={sort}&page={page}"
                          + (books ? "&books=true" : goods ? "&goods=true" : string.IsNullOrEmpty(slot) ? "" : "&slot=" + slot);
            yield return Send("GET", path, null, true, ApplyMarket, error => failure = error);
            done(failure);
        }

        /// <summary>A market call (list, buy, cancel); completes with (message, error).</summary>
        public IEnumerator MarketCall(string path, object request, Action<string, string> done)
        {
            string failure = null;
            yield return Post("/v1/market/" + path, JsonUtility.ToJson(request), true, ApplyMarket, error => failure = error);
            done(failure == null ? MarketView?.message : null, failure);
        }

        /// <summary>
        /// What a kind of thing sold for lately: query "kind=good&amp;id=5", "kind=book&amp;id=3" or
        /// "kind=piece&amp;slot=Weapon&amp;band=6&amp;plus=7&amp;rarity=Epic". Completes with the history, or null when it failed.
        /// </summary>
        public IEnumerator FetchPriceHistory(string query, Action<PriceHistoryDto> done)
        {
            PriceHistoryDto history = null;
            yield return Send("GET", "/v1/market/history?" + query, null, true, json => history = JsonUtility.FromJson<PriceHistoryDto>(json), _ => { });
            done(history);
        }

        private void ApplyMarket(string json)
        {
            MarketDto view = JsonUtility.FromJson<MarketDto>(json);
            MarketView = view;
            if (view.state != null && view.state.inventory != null && view.state.items != null) Apply(view.state);
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

        /// <summary>Timed world events (Rules.WorldEvents): what runs now and what comes within the week, as last sent.</summary>
        public WorldEventDto[] Events { get; private set; } = Array.Empty<WorldEventDto>();
        private float _eventsAt;
        /// <summary>Seconds until the event begins (0 once it runs), counted on from when the state came.</summary>
        public long EventStartsIn(WorldEventDto e) => Math.Max(0, e.startsInSeconds - (long)(Time.realtimeSinceStartup - _eventsAt));
        public long EventEndsIn(WorldEventDto e) => Math.Max(0, e.endsInSeconds - (long)(Time.realtimeSinceStartup - _eventsAt));
        public bool EventRunning(WorldEventDto e) => EventStartsIn(e) == 0 && EventEndsIn(e) > 0;

        public bool EventOn(WorldEventKind kind)
        {
            if (!Online) return false;
            foreach (WorldEventDto e in Events)
                if (e.kind == kind.ToString() && EventRunning(e)) return true;
            return false;
        }

        /// <summary>The lucky forge hour's extra chance while it runs (the server rolls with its own).</summary>
        public int ForgeLuckBp => EventOn(WorldEventKind.LuckyForge) ? WorldEvents.ForgeLuckBp : 0;
        private Bell _appliedBell = Rules.Bell.None;
        public Bell ActiveBell => _appliedBell;

        private void Apply(StateDto s)
        {
            DungeonRunsLeft = s.dungeonRunsLeft;
            DungeonRunAtSmith = s.dungeonRunAtSmith;
            // Older servers send no dungeon for the waiting run: it was the Hollow Spire's.
            DungeonPausedId = s.dungeonRunAtSmith != 0 ? (s.dungeonPausedId > 0 ? s.dungeonPausedId : 1) : 0;
            if (s.daily != null && s.daily.gifts != null && s.daily.gifts.Length > 0) Daily = s.daily;
            if (s.trail != null && s.trail.season > 0)
            {
                Trail = s.trail;
                _trailAt = Time.realtimeSinceStartup;
            }
            if (s.wardrobe != null && s.wardrobe.pieces != null)
            {
                Wardrobe = s.wardrobe;
                _wardrobeAt = Time.realtimeSinceStartup;
                _player.SetWorn(WornPieces());
            }
            _player.SetRenewals(s.renewals);
            // Skill grades (Rules.SkillGrades): all twelve by book id, the reads toward the next step, the rests, Honor.
            if (s.skillGrades != null && s.skillGrades.Length > 0)
            {
                SkillProgress = s.skillProgress ?? new int[Rules.Books.Count];
                SkillReadySeconds = s.skillReadySeconds ?? new long[Rules.Books.Count];
                SkillReadyAt = Time.realtimeSinceStartup;
                _player.SetSkillGrades(s.skillGrades);
            }
            Honor = s.honor;
            var inventory = new Inventory
            {
                Sorn = s.inventory.sorn, Potions = s.inventory.potions, Materials = s.inventory.materials,
                ScrollsOfMercy = s.inventory.scrollsOfMercy, KhansAlloys = s.inventory.khansAlloys,
                AnvilWards = s.inventory.anvilWards, Turnstones = s.inventory.turnstones,
                EtchingNeedles = s.inventory.etchingNeedles, SummoningMarkers = s.inventory.summoningMarkers, Xp = s.inventory.xp,
                HuntMarks = s.inventory.huntMarks, PinningWax = s.inventory.pinningWax, MastersNeedles = s.inventory.mastersNeedles, Oathstones = s.inventory.oathstones,
            };
            if (s.inventory.korshards != null) Array.Copy(s.inventory.korshards, inventory.Korshards, Math.Min(5, s.inventory.korshards.Length));
            if (s.inventory.books != null) Array.Copy(s.inventory.books, inventory.Books, Math.Min(Rules.Books.Count, s.inventory.books.Length));
            if (s.inventory.skins != null) inventory.Skins.AddRange(s.inventory.skins);
            if (s.inventory.fish != null) Array.Copy(s.inventory.fish, inventory.Fish, Math.Min(inventory.Fish.Length, s.inventory.fish.Length));
            if (s.inventory.pearls != null) Array.Copy(s.inventory.pearls, inventory.Pearls, Math.Min(3, s.inventory.pearls.Length));
            inventory.Mussels = s.inventory.mussels;
            inventory.GrandmasterNeedles = s.inventory.grandmasterNeedles;
            if (s.river != null)
            {
                River = s.river;
                RiverAt = Time.realtimeSinceStartup;
                // The Tireless Rod's catches since the last state, for the river screen's log.
                int auto = s.river.autoMussels;
                if (s.river.autoFish != null) foreach (int n in s.river.autoFish) auto += n;
                if (auto > 0) AutoCatches += auto;
            }
            if (s.bosses != null && s.bosses.Length > 0)
            {
                // Only /me and the heartbeat carry the Commanders, and with them the live trade (none: id 0).
                TradeBrief = s.trade != null && s.trade.id > 0 ? s.trade : null;
                FriendAsks = s.friendAsks;
                GuildInvites = s.guildInvites;
                WhisperUnread = s.whispers;
                MailUnread = s.mail;
                AchievementsReady = s.achievementsReady;
                if (s.goalCounts != null) GoalCounts = s.goalCounts;
                Title = s.title ?? "";
                Bosses = s.bosses;
                BossesReceivedAt = Time.realtimeSinceStartup;
            }
            if (s.events != null)
            {
                Events = s.events;
                _eventsAt = Time.realtimeSinceStartup;
                _player.SetForgeLuck(ForgeLuckBp);
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
            LastItems = s.items;
            ItemIds.Clear();
            var equipped = new List<ItemState>();
            foreach (ItemDto dto in s.items)
            {
                ItemState item = ToState(dto);
                ItemIds[item] = dto.id;
                if (dto.equipped) equipped.Add(item); else inventory.Loot.Add(item);
            }

            // The Bannerkin's pieces (Rules.Bannerkin): its own, out of the bag; they still go on the anvil by their id.
            var kinWorn = new List<ItemState>();
            if (s.kin != null)
            {
                Kin = s.kin;
                if (s.kin.worn != null)
                    foreach (ItemDto dto in s.kin.worn)
                    {
                        ItemState item = ToState(dto);
                        ItemIds[item] = dto.id;
                        kinWorn.Add(item);
                    }
                _player.SetKin(s.kin.joined, kinWorn);
            }
            _player.ApplyRemote(inventory, equipped, s.weaponsBroken, s.highestStageCleared, s.parkedStage);
            if (anvilId != null)
                foreach (KeyValuePair<ItemState, string> pair in ItemIds)
                    if (pair.Value == anvilId) { _player.PutOnAnvil(pair.Key); break; }
            if (!string.IsNullOrEmpty(s.heroClass) && Enum.TryParse(s.heroClass, out HeroClass cls)) _player.SetClass(cls);
            if (!string.IsNullOrEmpty(s.figure) && Enum.TryParse(s.figure, out Figure figure)) _player.Figure = figure;
            if (s.bounties != null && s.bounties.items != null)
            {
                Bounties = s.bounties;
                BountiesReceivedAt = Time.realtimeSinceStartup;
            }
            if (!string.IsNullOrEmpty(s.banner) && Enum.TryParse(s.banner, out Banner banner)) Banner = banner;
            if (!string.IsNullOrEmpty(s.name)) PlayerName = s.name;
            Guild = s.guild;
            Tallies = s.inventory.tallies;
            Email = s.email ?? "";
            EmailVerified = s.emailVerified;
            if (!string.IsNullOrEmpty(s.accountId)) AccountId = s.accountId;
            Logins = s.logins ?? new string[0];
            // The farm lane's seed: new on login and on every park; the lane then plays seeded loops the server replays.
            if (s.lane != null && ulong.TryParse(s.lane.seed, out ulong laneSeed)) _player.SetLaneSeed(laneSeed, s.lane.loop);
            Online = true;
            Status = "server: " + _baseUrl;
        }

        public static ItemState ToState(ItemDto dto)
        {
            var item = new ItemState(dto.itemLevel, (Rarity)Enum.Parse(typeof(Rarity), dto.rarity), (EquipSlot)Enum.Parse(typeof(EquipSlot), dto.slot), dto.kin)
            {
                UpgradeLevel = dto.upgradeLevel, PatienceBp = dto.patienceBp, LockedEtchingIndex = dto.lockedEtchingIndex,
                AverageDamagePercent = dto.averageDamage, SkillDamagePercent = dto.skillDamage, Temper = dto.temper,
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
        [Serializable] public class GuestLoginRequest { public string deviceToken; public bool lobby; }
        [Serializable] public class GuestLoginResponse { public string accountId; public string sessionToken; public bool created; public string loginId; public int characters; }
        [Serializable] public class CharacterSlotDto { public string id; public int slot; public string name; public string @class; public int level; public int armorBand; public int weaponBand; public int weaponUpgrade; public string skin; public int highestStageCleared; public string lastPlayedUtc; public string guildTag; public bool banned; public string figure; }
        [Serializable] public class LobbyDto { public string loginId; public CharacterSlotDto[] characters; public int maxSlots; public string banner; public long amber; public string email; public string message; public int links; public bool emailVerified; }
        [Serializable] public class CreateCharacterRequest { public string name; public string heroClass; public int slot; public string figure; }
        [Serializable] public class CharacterRequest { public string characterId; public string name; }
        [Serializable] public class DepotDto { public StateDto state; public ItemDto[] items; public int capacity; public string message; }
        [Serializable] public class DepotRequest { public string requestId; public string itemId; }
        [Serializable] public class ForgeRequest { public string requestId; public string method; public string slot; public string itemId; public bool pearl; }
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
        [Serializable] public class ItemDto { public string id; public string slot; public bool equipped; public string name; public int itemLevel; public string rarity; public int upgradeLevel; public int patienceBp; public int lockedEtchingIndex; public EtchingDto[] etchings; public SocketDto[] sockets; public int averageDamage; public int skillDamage; public bool kin; public bool kinWorn; public int temper; }
        [Serializable] public class TemperRequest { public string requestId; public string itemId; }
        [Serializable] public class TemperDto { public StateDto state; public bool success; public int before; public int after; public string message; }
        [Serializable] public class KinDto { public bool joined; public ItemDto[] worn; public int score; public int focusBp; public int focusSeconds; public int focusCooldownSeconds; public int healPercent; public int healCooldownSeconds; }
        [Serializable] public class KinWearRequest { public string requestId; public string itemId; }
        [Serializable] public class SocketInsertRequest { public string requestId; public string itemId; public int socketIndex; public string type; public int rank; }
        [Serializable] public class SocketClearRequest { public string requestId; public string itemId; public int socketIndex; }
        [Serializable] public class SocketResultDto { public bool success; public int socketIndex; public string text; }
        [Serializable] public class InventoryDto { public long sorn; public int potions; public int materials; public int scrollsOfMercy; public int khansAlloys; public int anvilWards; public int turnstones; public int etchingNeedles; public int summoningMarkers; public long xp; public int level; public int[] korshards; public string[] skins; public int huntMarks; public int pinningWax; public int tallies; public int mastersNeedles; public int oathstones; public int[] books; public int[] fish; public int mussels; public int[] pearls; public int grandmasterNeedles; }
        [Serializable] public class BountyDto { public int id; public string title; public string period; public long count; public int target; public int marks; public bool claimed; }
        [Serializable] public class BountyBoardDto { public BountyDto[] items; public int dailyResetSeconds; public int weeklyResetSeconds; }
        [Serializable] public class ClaimBountyRequest { public string requestId; public int bountyId; }
        [Serializable] public class ShopBuyRequest { public string requestId; public int shopItemId; public int count; }
        [Serializable] public class EtchRequest { public string requestId; public string itemId; }
        [Serializable] public class PinRequest { public string requestId; public string itemId; public int index; }
        [Serializable] public class EtchResultDto { public bool took; public int chanceBp; public string text; }
        [Serializable] public class BannerRequest { public string banner; }
        [Serializable] public class BannerStandingDto { public string banner; public string name; public long points; public int fortresses; }
        [Serializable] public class FortressDto { public int id; public string name; public string region; public string holder; public string phase; public long wall; public long wallMax; public long siegeEmber; public long siegeSky; public long siegeGold; public string lastEvent; public string flagGuild; }
        [Serializable] public class WarDto { public string season; public BannerStandingDto[] standings; public string lastWinner; public int mySornBonusPercent; public FortressDto[] fortresses; public int siegeCooldownSeconds; public KeepDto[] keeps; public string message; }
        [Serializable] public class KeepBidDto { public string tag; public string name; public string color; public long amount; public bool contender; public long damage; public bool mine; }
        [Serializable] public class KeepDto { public int fortressId; public string name; public string holderTag; public string holderName; public string holderColor; public int state; public int secondsToSiege; public int secondsLeft; public KeepBidDto[] bids; public long myBid; public bool contending; public bool holding; public bool canBid; public bool canFight; public long wall; public long mended; public string lastEvent; }
        [Serializable] public class KeepBidRequest { public string requestId; public int fortressId; public long amount; }
        [Serializable] public class KeepFightRequest { public string requestId; public int fortressId; }
        [Serializable] public class GuildWarFoeDto { public string tag; public string name; public string color; public int level; public int rating; }
        [Serializable] public class GuildWarLaneDto { public string name; public int front; public bool myFlag; public bool theirFlag; public bool broken; }
        [Serializable] public class GuildWarLadderDto { public string tag; public string name; public string color; public int rating; public int wins; public int losses; public int draws; public bool mine; }
        [Serializable] public class GuildWarDto { public int rating; public int wins; public int losses; public int draws; public string nextNight; public int secondsToNext; public bool signedUp; public int signedGuilds; public bool canSignUp; public bool canFlag; public bool atWar; public GuildWarFoeDto foe; public int myKills; public int theirKills; public int myScore; public int theirScore; public GuildWarLaneDto[] lanes; public int secondsLeft; public int fightsLeft; public int cooldownSeconds; public string lastEvent; public string lastResult; public GuildWarLadderDto[] ladder; public string message; }
        [Serializable] public class GuildWarSignupRequest { public bool join; }
        [Serializable] public class GuildWarFlagRequest { public int lane; }
        [Serializable] public class GuildWarFightRequest { public string requestId; public int lane; }
        [Serializable] public class DuelResultDto { public int lane; public ulong seed; public string champion; public long championHp; public long championAttack; public bool won; public int winChancePercent; public string text; public string defenderClass; public int defenderBand; public string defenderFigure; }
        [Serializable] public class GuildWarFightDto { public StateDto state; public DuelResultDto duel; public GuildWarDto war; }
        [Serializable] public class SiegeRequest { public string requestId; public int fortressId; }
        [Serializable] public class SiegeResultDto { public int fortressId; public int bossId; public bool defending; public ulong seed; public long damage; public int potionsAtStart; public string bell; public string phase; public long wallLeft; public bool phaseBroken; public bool captured; public string holder; public string text; }
        [Serializable] public class BossHitDto { public string name; public string banner; public long damage; }
        [Serializable] public class BossFightRequest { public string requestId; public int bossId; }
        [Serializable] public class BossStatusDto { public int bossId; public string name; public string mechanic; public bool up; public long secondsLeft; public bool foughtThisSpawn; public long hpLeft; public long hpMax; public bool slain; public string slainBy; public string slainBanner; public BossHitDto[] top; }
        [Serializable] public class BossFightResultDto { public int bossId; public ulong seed; public long damage; public bool killed; public int rank; public string chest; public int potionsAtStart; public string bell; public long poolLeft; public bool slew; }
        [Serializable] public class SettlementDto { public long countedSeconds; public long packs; public long korstones; public long sornEarned; public bool offline; public int activeBp; public int loopsVerified; public int leftBehind; }
        [Serializable] public class LaneDto { public string seed; public int loop; }
        [Serializable] public class CastDto { public int tick; public int skill; }
        [Serializable] public class LoopReportDto { public int loop; public int ticks; public int potions; public bool[] autoCast; public CastDto[] casts; }
        [Serializable] public class HeartbeatRequest { public LoopReportDto[] loops; }
        [Serializable] public class ForgeResultDto { public string outcome; public int chanceBp; public int levelBefore; public int levelAfter; }
        [Serializable] public class PushResultDto { public int stage; public bool cleared; public ulong seed; public int ticks; public int newHighestStageCleared; public int potionsAtStart; public string bell; }
        [Serializable] public class StateDto { public string accountId; public InventoryDto inventory; public ItemDto[] items; public int weaponsBroken; public int highestStageCleared; public int parkedStage; public BossStatusDto[] bosses; public BellDto bell; public SettlementDto settlement; public ForgeResultDto lastForge; public PushResultDto lastPush; public BossFightResultDto lastBossFight; public SocketResultDto lastSocket; public TurnResultDto lastTurn; public LaneDto lane; public string heroClass; public BountyBoardDto bounties; public string banner; public string name; public SiegeResultDto lastSiege; public EtchResultDto lastEtch; public GuildBriefDto guild; public string email; public string[] logins; public int dungeonRunsLeft; public long dungeonRunAtSmith; public WardrobeDto wardrobe; public TrailDto trail; public TradeBriefDto trade; public int dungeonPausedId; public int friendAsks; public int guildInvites; public int renewals; public int[] skillGrades; public int[] skillProgress; public long[] skillReadySeconds; public long honor; public int whispers; public string figure; public DailyDto daily; public int mail; public WorldEventDto[] events; public int achievementsReady; public string title; public bool emailVerified; public GoalCountsDto goalCounts; public RiverDto river; public KinDto kin; }
        [Serializable] public class RiverDto { public bool atRiver; public long[] mealSeconds; public long rodSecondsLeft; public int[] autoFish; public int autoMussels; }
        [Serializable] public class CastBiteDto { public int biteMs; public int windowMs; }
        [Serializable] public class ReelDto { public StateDto state; public string kind; public int fish = -1; public string message; }
        [Serializable] public class LandRequest { public bool landed; }
        [Serializable] public class OpenMusselsRequest { public string requestId; public int count; }
        [Serializable] public class OpenMusselsDto { public StateDto state; public int opened; public int[] pearls; public string message; }
        [Serializable] public class EatRequest { public string requestId; public int fish; }
        [Serializable] public class AutoRodRequest { public string requestId; public int days; }
        [Serializable] public class GoalCountsDto { public long commanders; public long dungeons; public long bounties; public int pitWins; }
        [Serializable] public class AchievementsDto { public StateDto state; public AchievementDto[] list; public int titleId; public string title; public string message; }
        [Serializable] public class AchievementDto { public int id; public string name; public string text; public long progress; public long target; public int honor; public long sorn; public string title; public bool done; public bool claimed; }
        [Serializable] public class AchievementClaimRequest { public string requestId; public int id; }
        [Serializable] public class TitleRequest { public int id; }
        [Serializable] public class GuildRaidDto { public string boss; public int map; public string mapName; public string mechanic; public long hpMax; public long hpLeft; public long secondsLeft; public int fightsLeft; public long myDamage; public RaidHitDto[] top; public bool slain; public string slainBy; public string message; }
        [Serializable] public class RaidHitDto { public string name; public long damage; }
        [Serializable] public class RaidFightRequest { public string requestId; }
        [Serializable] public class GuildRaidFightDto { public GuildRaidDto raid; public ulong seed; public long damage; public bool killed; public int potionsAtStart; public StateDto state; }
        [Serializable] public class WorldEventDto { public string kind; public string name; public string effect; public bool running; public long startsInSeconds; public long endsInSeconds; }
        [Serializable] public class DailyDto { public int day; public bool claimable; public string[] gifts; public long secondsToNext; }
        [Serializable] public class MailDto { public StateDto state; public LetterDto[] letters; public int unread; public string message; }
        /// <summary>A letter; goodId and bookId are -1 when it holds none (JsonUtility leaves a missing int at 0).</summary>
        [Serializable] public class LetterDto
        {
            public long id; public string kind; public string from; public string title; public string body; public string utc; public bool read; public bool taken;
            public long sorn; public int goodId = -1; public int goodCount; public int bookId = -1; public int bookCount; public ItemDto item;
        }
        [Serializable] public class MailTakeRequest { public string requestId; public long letterId; }
        [Serializable] public class MailDeleteRequest { public long letterId; }
        [Serializable] public class DailyClaimRequest { public string requestId; }
        [Serializable] public class WardrobePieceDto { public string id; public long secondsLeft; }
        [Serializable] public class WardrobeDto { public long amber; public WardrobePieceDto[] pieces; public string skin; public string mount; public string companion; public bool firstPurchase; }
        [Serializable] public class CaravanBuyRequest { public string requestId; public string pieceId; public int days; }
        [Serializable] public class WearRequest { public string requestId; public string pieceId; public string kind; }
        [Serializable] public class AmberPackRequest { public string requestId; public int packId; }
        [Serializable] public class TrailDto { public int season; public string name; public long secondsLeft; public long xp; public int tier; public int xpIntoTier; public int pass; public long freeClaimed; public long paidClaimed; public int owed; }
        [Serializable] public class TrailClaimRequest { public string requestId; public int tier; }
        [Serializable] public class TradeBriefDto { public long id; public string state; public bool incoming; public string otherName; }
        [Serializable] public class TradeDto { public long id; public string state; public bool incoming; public string otherName; public ItemDto[] myItems; public long mySorn; public string myStep; public ItemDto[] theirItems; public long theirSorn; public string theirStep; public int lockLeft; public int taxPercent; public bool rulesRelaxed; public string message; public StateDto hero; public BookOfferDto[] myBooks; public BookOfferDto[] theirBooks; }
        [Serializable] public class BookOfferDto { public int bookId; public int count; }
        [Serializable] public class TradeInviteRequest { public string requestId; public string name; public string accountId; }
        [Serializable] public class BannerChangeRequest { public string requestId; public string banner; }
        [Serializable] public class RenewRequest { public string requestId; }
        [Serializable] public class SkillTrainRequest { public string requestId; public int slot; }
        [Serializable] public class SkillTrainDto { public StateDto state; public int slot; public bool success; public int grade; public string message; }
        [Serializable] public class FriendDto { public string accountId; public string name; public string @class; public int level; public string banner; public string guildTag; public int minutesAway; }
        [Serializable] public class WhisperConversationDto { public string accountId; public string name; public string @class; public int level; public int minutesAway; public string lastText; public string lastUtc; public bool lastMine; public int unread; }
        [Serializable] public class WhispersDto { public WhisperConversationDto[] conversations; public int unread; public string message; }
        [Serializable] public class WhisperLineDto { public long id; public bool mine; public string text; public string utc; }
        [Serializable] public class WhisperThreadDto { public string accountId; public string name; public string @class; public int level; public int minutesAway; public WhisperLineDto[] lines; public long latest; public bool hasOlder; public bool blocked; public string message; }
        [Serializable] public class WhisperSendRequest { public string accountId; public string name; public string text; public long after; }
        [Serializable] public class WhisperReportRequest { public long messageId; }
        [Serializable] public class FriendsDto { public FriendDto[] friends; public FriendDto[] asking; public FriendDto[] asked; public int max; public bool canInvite; public string message; }
        [Serializable] public class FriendAddRequest { public string accountId; public string name; }
        [Serializable] public class FriendAnswerRequest { public string accountId; public bool accept; }
        [Serializable] public class FriendRemoveRequest { public string accountId; }
        [Serializable] public class GuildInviteRequest { public string requestId; public string accountId; public string name; }
        [Serializable] public class GuildInviteAnswerRequest { public string requestId; public string guildId; public bool accept; }
        [Serializable] public class TradeRequest { public string requestId; public long tradeId; }
        [Serializable] public class TradeOfferRequest { public string requestId; public long tradeId; public string[] itemIds; public long sorn; public BookOfferDto[] books; }
        [Serializable] public class TrailBuyRequest { public string requestId; public bool plus; }
        [Serializable] public class PitChallengerDto { public string id; public string name; public string tag; public int rating; public string league; public string @class; public string weapon; public int winChancePercent; public bool shade; }
        [Serializable] public class PitBoardDto { public int rank; public string name; public string tag; public int rating; public string league; public int wins; public int losses; public string weapon; public bool me; public string title; public string id; }
        [Serializable] public class InviteDto { public string code; public int invited; public int rewarded; public int maxInvited; public int rewardLevel; public long sorn; public int scrolls; public string invitedBy; public bool mineRewarded; public bool canEnter; public string message; }
        [Serializable] public class InviteRequest { public string code; }
        [Serializable] public class PitsDto { public int rating; public string league; public int wins; public int losses; public int laurels; public int ticketsLeft; public PitChallengerDto[] challengers; public PitBoardDto[] board; public string message; public int seasonWins; public int seasonLosses; public long seasonSecondsLeft; public string title; public int lastRank; public int lastRating; public int lastLaurels; public string lastChampions; }
        [Serializable] public class PitFightRequest { public string requestId; public string opponentId; }
        [Serializable] public class PitShopRequest { public string requestId; public int itemId; }
        [Serializable] public class PitFightDto { public StateDto state; public DuelResultDto duel; public PitsDto pits; public int ratingBefore; public int ratingAfter; public int laurelsGained; }
        [Serializable] public class DungeonEnterRequest { public string requestId; public int dungeonId; }
        [Serializable] public class DungeonSmithRequest { public string requestId; public long runId; public string itemId; public string rune; }
        [Serializable] public class DungeonFloorDto { public int floor; public ulong seed; public int potionsAtStart; public bool cleared; }
        [Serializable] public class DungeonResultDto { public StateDto state; public long runId; public int dungeonId; public int level; public DungeonFloorDto[] floors; public bool atSmith; public bool cleared; public int fellOn; public string chest; public ForgeResultDto smith; public string smithItem; public string text; public string pause; public string riddle; public string[] runes; }
        [Serializable] public class ProvidersDto { public string[] providers; }
        [Serializable] public class ExternalBeginRequest { public string provider; }
        [Serializable] public class ExternalBeginDto { public string url; }
        [Serializable] public class ExternalTicketRequest { public string ticket; public string deviceToken; }
        [Serializable] public class ExternalLoginResultDto { public string accountId; public string sessionToken; public bool switched; public bool linked; public string provider; }
        [Serializable] public class GuildBriefDto { public string tag; public string name; public string color; public string rank; }
        [Serializable] public class GuildDto { public string id; public string name; public string tag; public string color; public bool open; public int level; public long xp; public long nextLevelXp; public long treasury; public int plunder; public int muster; public int members; public int maxMembers; public int sornBonusPercent; public string[] fortresses; public string lastEvent; }
        [Serializable] public class GuildMemberDto { public string accountId; public string name; public string banner; public string rank; public int level; public long donated; public int lastSeenMinutes; public bool me; }
        [Serializable] public class GuildListItemDto { public string id; public string name; public string tag; public string color; public int level; public int members; public int maxMembers; public bool open; public bool requested; public string invitedBy; }
        /// <summary>mine is never null after JsonUtility: an empty id means no guild.</summary>
        [Serializable] public class GuildViewDto { public StateDto state; public GuildDto mine; public GuildMemberDto[] members; public GuildListItemDto[] browse; public long donatedToday; public long donationCap; public string message; public GuildMemberDto[] requests; public string[] log; public GuildListItemDto[] invites; public GuildMemberDto[] invited; }
        [Serializable] public class GuildAnswerRequest { public string requestId; public string accountId; public bool accept; }
        [Serializable] public class ChatLineDto { public long id; public string accountId; public string name; public string banner; public string text; public string utc; public bool system; public bool mine; public string title; }
        [Serializable] public class ChatDto { public string channel; public ChatLineDto[] lines; public long latestId; public int blocked; }
        [Serializable] public class ChatSayRequest { public string channel; public string text; public long after; }
        [Serializable] public class ChatReportRequest { public long messageId; public string channel; }
        [Serializable] public class ChatBlockRequest { public string accountId; public bool block; public string channel; }
        [Serializable] public class ListingDto { public long id; public ItemDto item; public long price; public string sellerName; public string sellerBanner; public bool mine; public int minutesLeft; public string status; public int bookId = -1; public int bookCount; public int goodId = -1; public int goodCount; }
        [Serializable] public class PriceHistoryDto { public string what; public int sales; public long average; public long low; public long high; public long last; public int lastMinutesAgo; public int days; public long[] recent; }
        [Serializable] public class MarketDto { public StateDto state; public ListingDto[] listings; public int page; public int pages; public int total; public ListingDto[] mine; public int taxPercent; public string message; }
        /// <summary>A scroll stack sends the empty Guid as itemId (the server reads it as a Guid).</summary>
        [Serializable] public class BagSellRequest { public string requestId; public string itemId; public string[] itemIds; }
        [Serializable] public class MarketListRequest { public string requestId; public string itemId; public long price; public int bookId = -1; public int bookCount; public int goodId = -1; public int goodCount; }
        [Serializable] public class MarketBuyRequest { public string requestId; public long listingId; }
        [Serializable] public class RegisterRequest { public string email; public string password; }
        [Serializable] public class LoginRequest { public string email; public string password; public string deviceToken; }
        [Serializable] public class ForgotRequest { public string email; }
        [Serializable] public class MilestoneRequest { public string name; }
        [Serializable] public class NameReportRequest { public string kind; public string accountId; }
        [Serializable] public class PushTokenRequest { public string platform; public string token; }
        [Serializable] public class LeaderRowDto { public int rank; public string id; public string name; public string title; public string @class; public int level; public string banner; public long value; public string tag; }
        [Serializable] public class LeaderboardDto { public string board; public string period; public LeaderRowDto[] rows; public LeaderRowDto mine; public string note; }
        [Serializable] public class InspectDto { public string id; public string name; public string title; public string @class; public string figure; public int level; public string banner;
            public string guildName; public string guildTag; public int highestStage; public int pitRating; public int pitWins; public string skin; public ItemDto[] worn; public bool banned; }
        [Serializable] public class AmberPurchaseRequest { public string store; public string productId; public string receipt; }
        [Serializable] public class AmberPurchaseDto { public StateDto state; public string message; public bool added; public long amber; }
        [Serializable] public class ResetRequest { public string email; public string code; public string password; public string deviceToken; }
        [Serializable] public class VerifyRequest { public string code; }
        [Serializable] public class MessageDto { public string message; public bool emailVerified; }
        [Serializable] public class GuildCreateRequest { public string requestId; public string name; public string tag; public string color; }
        [Serializable] public class GuildJoinRequest { public string requestId; public string guildId; }
        [Serializable] public class GuildLeaveRequest { public string requestId; }
        [Serializable] public class GuildMemberRequest { public string requestId; public string accountId; public string rank; }
        [Serializable] public class GuildDonateRequest { public string requestId; public long sorn; }
        [Serializable] public class GuildSkillRequest { public string requestId; public string skill; }
        [Serializable] public class GuildShopRequest { public string requestId; public int itemId; }
        [Serializable] public class GuildSettingsRequest { public string requestId; public bool open; public string color; }
    }
}
