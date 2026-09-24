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
const TABS = [["overview", "Overview"], ["reports", "Reports"], ["chat", "World chat"], ["players", "Players"], ["guilds", "Guilds"], ["log", "Log"]];

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

async function logView() {
  const log = await api("GET", "/log");
  if (!log.length) return el("div", { class: "empty" }, "No moderation yet.");
  return el("table", {}, ...log.map(a => el("tr", {},
    el("td", {}, new Date(a.utc).toLocaleString()), el("td", {}, a.admin), el("td", {}, a.action), el("td", {}, a.detail))));
}

$("signin").addEventListener("click", signIn);
$("password").addEventListener("keydown", (e) => { if (e.key === "Enter") signIn(); });
if (token) show();
