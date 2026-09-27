// Orsuun moderation page. Everything players wrote is shown with textContent, never as HTML.
"use strict";

let token = null;
try { token = sessionStorage.getItem("orsuunAdmin"); } catch (e) { token = null; }
let email = "";
let tab = "overview";
let search = { chat: "", players: "", guilds: "" };
let playerFilter = null; // { id, name } when the chat tab shows one player's lines

const $ = (id) => document.getElementById(id);

function el(tag, attrs, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs || {})) {
    if (k === "class") node.className = v;
    else if (k === "onclick") node.addEventListener("click", v);
    else node.setAttribute(k, v);
  }
  for (const c of children) {
    if (c == null || c === false) continue;
    node.appendChild(typeof c === "string" || typeof c === "number" ? document.createTextNode(String(c)) : c);
  }
  return node;
}

function say(text, bad) {
  const m = $("msg");
  m.textContent = text;
  m.style.color = bad ? "var(--bad)" : "var(--text)";
  m.style.display = "block";
  clearTimeout(say.timer);
  say.timer = setTimeout(() => { m.style.display = "none"; }, 3500);
}

async function api(method, path, body) {
  const res = await fetch("/admin/api" + path, {
    method,
    headers: { "Content-Type": "application/json", "X-Admin": token || "" },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (res.status === 401 && path !== "/login") { signOut("Your session ended. Sign in again."); throw new Error("signed out"); }
  let data = null;
  try { data = await res.json(); } catch (e) { data = null; }
  if (!res.ok) throw new Error((data && data.message) || ("Error " + res.status));
  return data;
}

function when(utc) {
  if (!utc) return "";
  const d = new Date(utc);
  const mins = Math.round((Date.now() - d.getTime()) / 60000);
  const ago = mins < 1 ? "just now" : mins < 60 ? mins + "m ago" : mins < 1440 ? Math.round(mins / 60) + "h ago" : Math.round(mins / 1440) + "d ago";
  return d.toLocaleString() + " (" + ago + ")";
}

function until(utc) {
  const mins = Math.round((new Date(utc).getTime() - Date.now()) / 60000);
  return mins < 60 ? mins + " min" : mins < 1440 ? Math.round(mins / 60) + " h" : Math.round(mins / 1440) + " days";
}

async function act(fn, done) {
  try { await fn(); if (done) say(done); render(); } catch (e) { if (e.message !== "signed out") say(e.message, true); }
}

// ---- sign in ----
async function signIn() {
  $("loginError").textContent = "";
  try {
    const r = await api("POST", "/login", { email: $("email").value, password: $("password").value });
    token = r.token; email = r.email;
    try { sessionStorage.setItem("orsuunAdmin", token); sessionStorage.setItem("orsuunAdminEmail", email); } catch (e) { /* private mode */ }
    $("password").value = "";
    show();
  } catch (e) { $("loginError").textContent = e.message; }
}

function signOut(reason) {
  token = null;
  try { sessionStorage.removeItem("orsuunAdmin"); } catch (e) { /* ignore */ }
  $("app").hidden = true;
  $("login").hidden = false;
  if (reason) $("loginError").textContent = reason;
}

function show() {
  try { email = email || sessionStorage.getItem("orsuunAdminEmail") || ""; } catch (e) { /* ignore */ }
  $("login").hidden = true;
  $("app").hidden = false;
  $("who").textContent = "Signed in as " + email + " · sessions last 12 hours";
  render();
}

// ---- tabs ----
const TABS = [["overview", "Overview"], ["reports", "Reports"], ["names", "Names"], ["chat", "World chat"], ["players", "Players"], ["guilds", "Guilds"], ["events", "Events"],
  ["funnel", "Funnel"], ["errors", "Errors"], ["log", "Log"]];

function tabs(openReports) {
  const nav = $("tabs");
  nav.replaceChildren(...TABS.map(([key, label]) =>
    el("button", { class: tab === key ? "on" : "", onclick: () => { tab = key; if (key !== "chat") playerFilter = null; render(); } },
      label + (key === "reports" && openReports ? " (" + openReports + ")" : ""))),
    el("button", { onclick: () => signOut("Signed out.") }, "Sign out"));
}

async function render() {
  const view = $("view");
  try {
    const overview = await api("GET", "/overview");
    tabs(overview.openReports);
    if (tab === "overview") view.replaceChildren(overviewView(overview));
    else if (tab === "reports") view.replaceChildren(await reportsView());
    else if (tab === "chat") view.replaceChildren(await chatView());
    else if (tab === "players") view.replaceChildren(await playersView());
    else if (tab === "guilds") view.replaceChildren(await guildsView());
    else if (tab === "events") view.replaceChildren(await eventsView());
    else if (tab === "funnel") view.replaceChildren(await funnelView());
    else if (tab === "names") view.replaceChildren(await namesView());
    else if (tab === "errors") view.replaceChildren(await errorsView());
    else view.replaceChildren(await logView());
  } catch (e) {
    if (e.message !== "signed out") view.replaceChildren(el("div", { class: "empty" }, e.message));
  }
}

function overviewView(o) {
  const stat = (n, label) => el("div", { class: "stat" }, el("b", {}, n), label);
  return el("div", {},
    el("div", { class: "grid" },
      stat(o.openReports, "open reports"), stat(o.players, "players"), stat(o.active24h, "active in 24 h"), stat(o.new24h, "new in 24 h"),
      stat(o.withEmail, "with an account email"), stat(o.chat24h, "chat lines in 24 h"), stat(o.guilds, "guilds"),
      stat(o.activeListings, "Exchange listings"), stat(o.muted, "muted now"), stat(o.banned, "banned")),
    el("p", { class: "meta" }, "Three reports hide a line by themselves. Work the Reports tab: hide what breaks the rules, keep what does not, and mute or ban repeat offenders."));
}

function lineCard(l, withPlayer) {
  const tags = [];
  if (l.reports) tags.push(el("span", { class: "tag warn" }, l.reports + " report" + (l.reports === 1 ? "" : "s")));
  if (l.hidden) tags.push(el("span", { class: "tag bad" }, "hidden"));
  if (l.channel === "guild") tags.push(el("span", { class: "tag" }, "guild chat"));
  const player = l.accountId !== "00000000-0000-0000-0000-000000000000";
  return el("div", { class: "card" + (l.hidden ? " hidden" : "") },
    el("div", { class: "meta" }, el("b", {}, l.name), ...tags, " · ", when(l.utc)),
    el("div", { class: "text" }, l.text),
    el("div", { class: "actions" },
      l.hidden
        ? el("button", { class: "good", onclick: () => act(() => api("POST", "/lines/" + l.id, { action: "show" }), "Line shown again.") }, "Show again")
        : el("button", { class: "bad", onclick: () => act(() => api("POST", "/lines/" + l.id, { action: "hide" }), "Line hidden.") }, "Hide"),
      l.reports && !l.reviewed ? el("button", { onclick: () => act(() => api("POST", "/lines/" + l.id, { action: "dismiss" }), "Reports dismissed.") }, "Reports are wrong: keep") : null,
      withPlayer && player ? el("button", { onclick: () => { playerFilter = { id: l.accountId, name: l.name }; tab = "chat"; render(); } }, "All their lines") : null,
      withPlayer && player ? el("button", { onclick: () => { search.players = l.accountId; tab = "players"; render(); } }, "Player…") : null));
}

async function reportsView() {
  const lines = await api("GET", "/reports");
  if (!lines.length) return el("div", { class: "empty" }, "No open reports. Well kept steppe.");
  return el("div", {}, ...lines.map(l => lineCard(l, true)));
}

function searchRow(key, placeholder, extra) {
  const input = el("input", { placeholder, value: search[key] });
  input.addEventListener("keydown", (e) => { if (e.key === "Enter") { search[key] = input.value; render(); } });
  return el("div", { class: "row" }, input, el("button", { onclick: () => { search[key] = input.value; render(); } }, "Search"), extra || null);
}

async function chatView() {
  let path = "/chat?q=" + encodeURIComponent(search.chat);
  if (playerFilter) path += "&accountId=" + playerFilter.id;
  const lines = await api("GET", path);
  return el("div", {},
    playerFilter ? el("p", { class: "meta" }, "All lines of " + playerFilter.name + " (every channel). ",
      el("button", { onclick: () => { playerFilter = null; render(); } }, "Back to world chat")) : null,
    searchRow("chat", "Search words or names"),
    lines.length ? el("div", {}, ...lines.map(l => lineCard(l, !playerFilter))) : el("div", { class: "empty" }, "Nothing here."));
}

async function playersView() {
  const players = await api("GET", "/players?q=" + encodeURIComponent(search.players));
  const rows = players.map(p => {
    const status = [];
    if (p.bannedUtc) status.push(el("span", { class: "tag bad" }, "banned" + (p.banReason ? ": " + p.banReason : "")));
    if (p.mutedUntilUtc) status.push(el("span", { class: "tag warn" }, "muted " + until(p.mutedUntilUtc)));
    if (p.reportedLines) status.push(el("span", { class: "tag warn" }, p.reportedLines + " reported line" + (p.reportedLines === 1 ? "" : "s")));
    const mute = (minutes, label) => el("button", {
      onclick: () => {
        const reason = prompt("Mute " + p.name + " for " + label + ". Reason (for the log):", "chat rules");
        if (reason !== null) act(() => api("POST", "/players/" + p.id + "/mute", { minutes, reason }), p.name + " muted for " + label + ".");
      },
    }, "Mute " + label);
    return el("div", { class: "card" + (p.bannedUtc ? " hidden" : "") },
      el("div", { class: "meta" }, el("b", {}, p.name), p.guild ? " [" + p.guild + "]" : "", " · ", p.banner === "None" ? "no Banner" : p.banner, " · level ", p.level,
        p.email ? " · " + p.email : " · guest", ...status),
      el("div", { class: "meta" }, "Last seen " + when(p.lastSeenUtc) + " · joined " + when(p.createdUtc) + " · id " + p.id),
      el("div", { class: "actions" },
        el("button", { onclick: () => { playerFilter = { id: p.id, name: p.name }; tab = "chat"; render(); } }, "Their lines"),
        p.mutedUntilUtc
          ? el("button", { class: "good", onclick: () => act(() => api("POST", "/players/" + p.id + "/mute", { minutes: 0 }), "Unmuted.") }, "Unmute")
          : null,
        p.mutedUntilUtc ? null : mute(60, "1 hour"),
        p.mutedUntilUtc ? null : mute(1440, "24 hours"),
        p.mutedUntilUtc ? null : mute(10080, "7 days"),
        p.bannedUtc
          ? el("button", { class: "good", onclick: () => { if (confirm("Unban " + p.name + "?")) act(() => api("POST", "/players/" + p.id + "/unban"), "Unbanned."); } }, "Unban")
          : el("button", {
            class: "bad",
            onclick: () => {
              const reason = prompt("Ban " + p.name + ". They cannot sign in, their Exchange listings close, they leave their guild and their chat lines are hidden. Reason (they see it):", "");
              if (reason) act(() => api("POST", "/players/" + p.id + "/ban", { reason, hideLines: true }), p.name + " banned.");
            },
          }, "Ban")));
  });
  return el("div", {}, searchRow("players", "Name, email, account id or guild tag"),
    rows.length ? el("div", {}, ...rows) : el("div", { class: "empty" }, "No players found."));
}

async function guildsView() {
  const guilds = await api("GET", "/guilds?q=" + encodeURIComponent(search.guilds));
  const rows = guilds.map(g => el("div", { class: "card" },
    el("div", { class: "meta" }, el("b", {}, "[" + g.tag + "] " + g.name), " · level ", g.level, " · ", g.members, g.members === 1 ? " member · leader " : " members · leader ",
      g.leader || "none", g.open ? "" : " · gates shut"),
    el("div", { class: "meta" }, "Founded " + when(g.createdUtc)),
    el("div", { class: "actions" },
      el("button", {
        onclick: () => {
          const name = prompt("New name for [" + g.tag + "] " + g.name + " (3 to 20 characters):", g.name);
          if (name === null) return;
          const tag = prompt("New tag (2 to 4 capital letters or digits):", g.tag);
          if (tag !== null) act(() => api("POST", "/guilds/" + g.id + "/rename", { name, tag }), "Guild renamed.");
        },
      }, "Rename"),
      el("button", {
        class: "bad",
        onclick: () => { if (confirm("Disband [" + g.tag + "] " + g.name + "? Every member leaves and its treasury is lost.")) act(() => api("POST", "/guilds/" + g.id + "/disband"), "Guild disbanded."); },
      }, "Disband"))));
  return el("div", {}, searchRow("guilds", "Guild name or tag"), rows.length ? el("div", {}, ...rows) : el("div", { class: "empty" }, "No guilds."));
}

// ---- weekend events: the server's calendar (times are server time) ----
const EVENT_KINDS = [["DoubleSorn", "Double Sorn Weekend"], ["LuckyForge", "Lucky Forge Hour"], ["CommanderRush", "Commander Rush"]];

async function eventsView() {
  const events = await api("GET", "/events");
  const kind = el("select", {}, ...EVENT_KINDS.map(([k, label]) => el("option", { value: k }, label)));
  const start = el("input", { type: "text", placeholder: "2026-10-10 20:00" });
  const hours = el("input", { type: "number", min: "1", max: "168", value: "2" });
  const add = el("div", { class: "card" },
    el("div", { class: "meta" }, "Put an event on the calendar. The start is server time (Europe/Istanbul); it is announced in world chat when it begins."),
    el("div", { class: "row" }, kind, start, hours,
      el("button", { class: "good", onclick: () => act(() => api("POST", "/events", { kind: kind.value, startsLocal: start.value, hours: parseInt(hours.value, 10) || 0 }), "Event added.") }, "Add")));
  const now = Date.now();
  const rows = events.map(e => {
    const running = new Date(e.startsUtc).getTime() <= now && now < new Date(e.endsUtc).getTime();
    const tags = [];
    if (e.weekly) tags.push(el("span", { class: "tag" }, "weekly"));
    if (running && !e.cancelled) tags.push(el("span", { class: "tag warn" }, "running"));
    if (e.cancelled) tags.push(el("span", { class: "tag bad" }, "called off"));
    if (e.announced) tags.push(el("span", { class: "tag" }, "announced"));
    return el("div", { class: "card" + (e.cancelled ? " hidden" : "") },
      el("div", { class: "meta" }, el("b", {}, e.name), ...tags),
      el("div", { class: "meta" }, e.startsLocal + " to " + e.endsLocal + " server time · set by " + e.by),
      el("div", { class: "actions" }, e.cancelled
        ? el("button", { class: "good", onclick: () => act(() => api("POST", "/events/" + e.id + "/on"), "Event back on.") }, "Put back on")
        : el("button", { class: "bad", onclick: () => { if (confirm("Call off " + e.name + " (" + e.startsLocal + ")?")) act(() => api("POST", "/events/" + e.id + "/off"), "Event called off."); } }, "Call off")));
  });
  return el("div", {}, add, rows.length ? el("div", {}, ...rows) : el("div", { class: "empty" }, "Nothing on the calendar."));
}

// ---- reported names (27 Sep 2026): keep, rename or ban ----
async function namesView() {
  const list = await api("GET", "/names");
  if (!list.length) return el("div", { class: "empty" }, "No reported names waiting.");
  return el("div", {},
    el("p", { class: "meta" }, "Names players reported, most reported first. Keep a fair name, rename one that breaks the rules (the hero gets a letter), or ban."),
    ...list.map(n => {
      const shown = n.kind === "guild" ? n.name + " [" + n.tag + "]" : n.name;
      const buttons = [el("button", { class: "good", onclick: () => act(() => api("POST", "/names/keep", { kind: n.kind, targetId: n.targetId }), "Kept.") }, "Keep")];
      if (n.kind === "hero") {
        buttons.push(el("button", { onclick: () => {
          const name = prompt("New name for " + n.name + " (3 to 16 letters or digits):", "");
          if (name) act(() => api("POST", "/players/" + n.targetId + "/rename", { name }), "Renamed.");
        } }, "Rename"));
        if (!n.banned) buttons.push(el("button", { class: "bad", onclick: () => {
          const reason = prompt("Why is " + n.name + " banned? (shown to them)", "An offensive name.");
          if (reason) act(() => api("POST", "/players/" + n.targetId + "/ban", { reason, hideLines: true }), n.name + " banned.");
        } }, "Ban"));
      } else {
        buttons.push(el("button", { onclick: () => {
          const name = prompt("New name for [" + n.tag + "] " + n.name + " (3 to 20 characters):", "");
          if (name === null || name === "") return;
          const tag = prompt("New tag (2 to 4 capital letters or digits):", n.tag);
          if (tag !== null) act(() => api("POST", "/guilds/" + n.targetId + "/rename", { name, tag }), "Guild renamed.");
        } }, "Rename"));
      }
      return el("div", { class: "card" },
        el("div", { class: "meta" }, el("span", { class: "tag" }, n.kind === "guild" ? "guild" : "hero"),
          el("span", { class: "tag warn" }, n.reports + (n.reports === 1 ? " report" : " reports")), n.banned ? el("span", { class: "tag bad" }, "banned") : null,
          " · last " + when(n.lastUtc)),
        el("div", { class: "text" }, shown),
        el("div", { class: "row" }, ...buttons));
    }));
}

// ---- funnel and errors (tester analytics, 27 Sep 2026) ----
let funnelDays = 7;

function minutes(m) {
  if (!m) return "";
  return m < 60 ? Math.round(m) + " min" : m < 1440 ? (m / 60).toFixed(1) + " h" : (m / 1440).toFixed(1) + " days";
}

function headRow(cols) { return el("tr", {}, ...cols.map(c => el("th", {}, c))); }

async function funnelView() {
  const f = await api("GET", "/funnel?days=" + funnelDays);
  const rows = (list, base, timed) => list.map(s => el("tr", {},
    el("td", {}, s.label), el("td", {}, String(s.count)), el("td", {}, base ? Math.round(100 * s.count / base) + "%" : "-"),
    el("td", {}, timed ? minutes(s.medianMinutes) : "")));
  const installs = f.wayIn.length ? f.wayIn[0].count : 0;
  return el("div", {},
    el("div", { class: "row" }, "Made in the last",
      ...[1, 7, 30].map(d => el("button", { class: funnelDays === d ? "on" : "", onclick: () => { funnelDays = d; render(); } }, d === 1 ? "day" : d + " days"))),
    el("h3", {}, "The way in"),
    el("table", {}, headRow(["", "logins", "of installs", ""]), ...rows(f.wayIn, installs, false)),
    el("h3", {}, f.heroes + " heroes made"),
    el("table", {}, headRow(["", "heroes", "of heroes", "median time from making"]), ...rows(f.steps, f.heroes, true)),
    el("p", { class: "meta" }, "Each first is written once per hero when it happens (Korstones, Forge, pushes, bounties, Commanders, dungeons and sieges count from 27 Sep 2026). The guide's steps come from the phones."));
}

async function errorsView() {
  const list = await api("GET", "/errors");
  if (!list.length) return el("div", { class: "empty" }, "No errors from the phones in the last week.");
  return el("div", {}, ...list.map(e => el("div", { class: "card" },
    el("div", { class: "meta" }, el("b", {}, e.count + " x"), " on " + e.heroes + (e.heroes === 1 ? " hero" : " heroes"),
      " · " + e.platforms + " · " + e.versions + " · last " + when(e.lastUtc)),
    el("div", { class: "text" }, e.message),
    e.stack ? el("details", {}, el("summary", {}, "Stack"), el("pre", {}, e.stack)) : null)));
}

async function logView() {
  const log = await api("GET", "/log");
  if (!log.length) return el("div", { class: "empty" }, "No moderation yet.");
  return el("table", {}, ...log.map(a => el("tr", {},
    el("td", {}, new Date(a.utc).toLocaleString()), el("td", {}, a.admin), el("td", {}, a.action), el("td", {}, a.detail))));
}

$("signin").addEventListener("click", signIn);
$("password").addEventListener("keydown", (e) => { if (e.key === "Enter") signIn(); });
if (token) show();
