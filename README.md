# Orsuun: War of Banners

Side-scrolling idle auto-battler with a risky upgrade economy. Design lives in the GDD; this repo holds the code.

## Layout

| Path | What it is |
| --- | --- |
| `src/Orsuun.Rules` | Engine-free game rules: Forge, etchings and Turnstones, offline rewards. `netstandard2.1`, C# 9, no Unity or server dependencies. Also a Unity local package (`package.json` + `.asmdef`). |
| `tests/Orsuun.Rules.Tests` | xUnit tests. They pin the numbers the GDD publishes to players. |
| `tools/Orsuun.Sim` | Monte Carlo balance simulator. Run it after every rate change. |
| `client/` | Unity 6 (6000.0.32f1) grey-box: one lane, a Vanguard, mob packs, a Korstone with waves, and the Forge screen. Everything is built in code by `GameRoot`; the scene is empty on purpose. |
| `tools/screenshot.ps1` | Launches the Windows build and saves a PNG of its window. |

## Rules of the rules library

- **Server authority.** The server runs this code to decide outcomes. The client runs the same code only to display and predict.
- **No floats in gameplay.** Chances are basis points (10000 = 100%), sorn is `long`. `ForgeAnalysis` uses doubles and is for balance work only.
- **No hidden randomness.** Every roll takes an `IRandom`. The server passes a cryptographic generator, tests and replays pass `XorShiftRandom` with a seed.
- **Rules decide, callers pay.** `ForgeService.Attempt` returns the outcome; charging sorn, materials and the scroll is an inventory transaction in the caller.

## Commands

The .NET 8 SDK is installed per user in `%LOCALAPPDATA%\Microsoft\dotnet`. If `dotnet` on PATH reports no SDK, call that one directly or put it first on PATH.

```powershell
$dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
& $dotnet test -c Release
& $dotnet run --project tools/Orsuun.Sim -c Release            # 200,000 runs, fixed seed
& $dotnet run --project tools/Orsuun.Sim -c Release 50000 42   # runs, seed
```

## Playing the grey-box

- Double-click `client\Builds\Windows\Orsuun.exe` (not in git; rebuild with the command below), or open `client/` in Unity Hub and press Play in `Assets/Orsuun/Scenes/Main.unity`.
- Skills are the three big buttons, each with its own AUTO toggle. FORGE opens the Forge while the hunt continues behind it. SPEED cycles x1, x3, x8. DEV grants sorn and consumables so a tester can reach +7 to +9 in one sitting.
- `Orsuun.exe -forge` starts with the Forge open.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Unity.exe"
# First-time setup (scene, player settings, GreyBox material):
& $unity -batchmode -quit -projectPath client -executeMethod Orsuun.Client.EditorTools.ProjectSetup.Run -logFile artifacts\unity-setup.log
# Play-mode smoke tests:
& $unity -batchmode -projectPath client -runTests -testPlatform PlayMode -testResults artifacts\playmode-results.xml -logFile artifacts\unity-playmode.log
# Windows playtest build:
& $unity -batchmode -quit -projectPath client -executeMethod Orsuun.Client.EditorTools.ProjectSetup.BuildWindows -logFile artifacts\unity-build.log
```

Grey-box shortcuts that are not the final design: built-in render pipeline instead of URP, a time-seeded local RNG instead of server rolls, a weapon that arrives with 5 etchings, and one hard-coded stage.

## Using the rules in Unity

In the client project's `Packages/manifest.json`:

```json
"com.orsuun.rules": "file:../../src/Orsuun.Rules"
```

Build output goes to `/artifacts` (see `Directory.Build.props`), so the package folder contains only source.
