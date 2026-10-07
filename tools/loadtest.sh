#!/bin/bash
# Load test with the server's CPU measured (owner, 7 Oct 2026: "Load test before more testers"). Runs tools/LoadTest
# against a server copy on port 5090 and prints, besides its latency table, how much of one CPU core the server and
# PostgreSQL used while every hero was in. The playtest box has one vCPU about as fast as one core of this Mac, so the
# "together" figure is the share of the box the same crowd would take (HANDOFF.md, "Load test and tester cap").
#
#   bash tools/loadtest.sh <heroes> <minutes> [LoadTest options, e.g. --dormant 1000 --noloops]
#
# Never point it at the playtest server: it makes guest heroes and uses the dev tools. A copy (Mac): the newest playtest
# backup in a database of its own, and the server built apart and started on port 5090 as a one-CPU runtime like the
# box's (DOTNET_PROCESSOR_COUNT=1: with eight cores the runtime's spinning and garbage collector cost about twice the CPU):
#   createdb -h localhost -U orsuun orsuun_load
#   gunzip -c ~/orsuun-backups/<newest>.sql.gz | psql -q -h localhost -U orsuun orsuun_load
#   dotnet build src/Orsuun.Server -c Release -o <dir>; cd <dir>
#   DOTNET_PROCESSOR_COUNT=1 URLS=http://localhost:5090 ASPNETCORE_ENVIRONMENT=Development \
#     ConnectionStrings__Game="Host=localhost;Port=5432;Database=orsuun_load;Username=orsuun;Password=orsuun-dev" dotnet Orsuun.Server.dll
# Run a short one first (the runtime warms up); a 200-hero run takes about five minutes with setting up and clearing away.
set -e
HEROES=${1:-100}; MINUTES=${2:-3}; shift 2 || true
export PATH="$HOME/.dotnet:$PATH"
cd "$(dirname "$0")/.."
SERVER=$(lsof -nP -iTCP:5090 -sTCP:LISTEN -t | head -1)
[ -n "$SERVER" ] || { echo "No server copy listening on port 5090 (see the top of this script)."; exit 1; }
WORK=$(mktemp -d)
dotnet build tools/LoadTest -c Release -v q > "$WORK/build.txt" || { cat "$WORK/build.txt"; exit 1; }

# Every 5 s, each process's CPU seconds: the server ("s") and every postgres process ("p").
sample() {
  while true; do
    ps -A -o pid=,time=,comm= | awk -v srv="$SERVER" -v now="$(date +%s)" '
      function secs(t,  a, n) { n = split(t, a, ":"); return n == 3 ? a[1]*3600 + a[2]*60 + a[3] : a[1]*60 + a[2] }
      $1 == srv { print now, $1, secs($2), "s" } $3 ~ /postgres/ { print now, $1, secs($2), "p" }'
    sleep 5
  done
}
sample > "$WORK/cpu.txt" &
SAMPLER=$!
trap 'kill $SAMPLER 2>/dev/null' EXIT

dotnet run -c Release --no-build --project tools/LoadTest -- --url http://localhost:5090 --heroes "$HEROES" --minutes "$MINUTES" "$@" |
while IFS= read -r line; do
  case "$line" in
    *"Running for"*) date +%s > "$WORK/start" ;;
    *"heroes,"*) date +%s > "$WORK/end"; ps -o rss= -p "$SERVER" | awk '{ printf "server memory %.0f MB\n", $1 / 1024 }' ;;
  esac
  echo "$line"
done

# The steady window: from 40 s after the run began (the heroes ramp in over 30 s) to its end. A process seen first inside
# the window counts from 0; one that ends loses at most its last 5 s.
python3 - "$WORK/cpu.txt" "$(( $(cat "$WORK/start") + 40 ))" "$(cat "$WORK/end")" <<'EOF'
import sys
f, a, b = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
rows = [l.split() for l in open(f) if l.strip()]
times = sorted({int(r[0]) for r in rows if a <= int(r[0]) <= b})
last, used = {}, {"s": 0.0, "p": 0.0}
for t, pid, cpu, kind in rows:
    t, cpu = int(t), float(cpu)
    if not times or t < times[0] or t > times[-1]: continue
    if (kind, pid) in last: used[kind] += max(0.0, cpu - last[(kind, pid)])
    elif t > times[0]: used[kind] += cpu
    last[(kind, pid)] = cpu
d = max(1, times[-1] - times[0]) if times else 1
print(f"CPU over {d} s: server {100 * used['s'] / d:.0f}% of a core, postgres {100 * used['p'] / d:.0f}%, together {100 * (used['s'] + used['p']) / d:.0f}%")
EOF
