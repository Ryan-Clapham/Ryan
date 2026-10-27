# Colour Cascade: Testing Report and Checklist

## What was verified, and how

The game was built in an environment **without the Unity Editor** (it needs a licensed install), so nothing
here involved pressing Play, a Unity build or a phone. Verification instead used .NET 8 and Unity's real
reference assemblies:

| Check | Tool | Result |
|---|---|---|
| Game logic compiles against .NET Standard 2.1 (Unity's API surface), warnings as errors | `dotnet build CoreLib`, `ProceduralLib` | 0 errors, 0 warnings |
| Every Unity runtime script compiles against **Unity 2021.3 engine + uGUI reference assemblies**, warnings as errors | `dotnet build UnityCheck` | 0 errors, 0 warnings |
| Same, with the `UNITY_ANDROID` code paths enabled | `UnityCheck -p:UnityPlatform=Android` | 0 errors, 0 warnings * |
| Same, for a project using **only the new Input System** | `UnityCheck -p:UnityPlatform=InputSystem` | 0 errors, 0 warnings |
| Editor setup and build scripts compile against the UnityEditor reference | `dotnet build EditorCheck` | 0 errors |
| Automated tests (NUnit, identical to Unity's EditMode tests) | `dotnet test Tests` | **145 passed, 0 failed** |
| Unity message methods (`Start`, `Update`…) have the signatures Unity accepts; no saved field has a type Unity can't serialize (UAC1001); every character in on-screen text exists in the Fredoka font | `dotnet run --project UnityLint` | No problems |
| Balance: bots play campaign levels 1–130, Endless, Weekly Rush and 7 Daily Challenges with the real engine | `Sim -- balance` | See DESIGN.md §3–6 |
| Economy: a simulated player earns coins for 30 days through the real reward code | `Sim -- economy` | See DESIGN.md §8 |
| Art: every generated sprite and a composited mock board exported to PNG and inspected | `Sim -- art` | Reviewed; symbols changed after review (§ below) |
| Audio: every effect and the music loop synthesised and checked for NaN/clipping/silence | tests + `Sim -- art` | Pass |

\* The Android reference set omits the mobile-only `Handheld` API, so a harness-only shim declares it;
real Android builds provide it.

### Automated test coverage (145 tests)

| Area | Tests | Highlights |
|---|---|---|
| Scoring | 10 | 2→40, 3→90, 4→160, 5→250, 6→360; configurable formula; praise tiers; star thresholds |
| Group detection | 6 | Orthogonal only, diagonals don't connect, stones/voids break groups, buffer reuse |
| Gravity and refill | 6 | Ids preserved, stones stop falls, **independent sections**, voids as barriers |
| Dead-board recovery | 7 | Reshuffle keeps the same tiles, stones never move, constructive fallback, graceful failure, determinism |
| Sessions and modes | 14 | Win/lose/revive, Daily plays all moves, Endless bonus moves, Rush timer start/stop/expiry/pause, **only Weekly Rush is timed**, long randomised play keeps board invariants |
| Power-ups | 17 | All six effects and their invalid targets, stones vs Blast/Tile Breaker, trial-first consumption, nothing consumed on failure, **challenge modes blocked at engine and service level** |
| Wallet, shop, rewards | 22 | Ledger, insufficient funds, locked items, **purchases blocked during challenge runs**, first-clear once, new stars only, milestones once, daily/weekly/endless claims once, clock rollback refused, early-economy sanity |
| Progression | 9 | **Board sizes 6×6 / 7×7 / 8×9**, 120 generated levels valid and deterministic, stones from level 8, early levels ≥80% winnable by a casual bot, replays never reduce stars |
| Challenges | 15 | UTC day keys, ISO weeks (including year boundaries), Monday reset, same board and refills for the same date/week, different days differ, **Endless fresh board each run** |
| Saves | 10 | Full round trip, missing/corrupt/backup/temp recovery on a real file system, write failure, repair of inconsistent data, reload through the context, reset rebinding |
| Navigation | 5 | Back stops at Home, no duplicates, no loops, leaving a game replaces it, Home clears history |
| Procedural assets | 6 | Atlas has every referenced sprite with no overlaps, cache round trip, **symbols measurably distinct**, valid audio |
| Regression checklist | 13 | One test per logic item in the brief's §21 list |
| Project setup | 2 | `Packages/manifest.json` declares every package and module the assemblies need |

### Defects found and fixed during verification

* Symbols: the original blue circle and purple diamond/square were too similar to the heart (measured).
  Blue became a ring and purple a crescent moon (now ≥ 0.46 Jaccard distance from every other shape).
* **Found in later Unity sessions:** shop buttons showed no prices (an empty button label never got a Text);
  warning UAC1001 for `TileView.Cell` (a runtime-only field Unity tried to serialize; now `[NonSerialized]`, and
  `UnityLint` checks for the rest); Unity opened an empty "Untitled" scene (only a sky in the Game view) instead
  of the game's scene, which now opens automatically.
* **Found in the first real Unity sessions (6000.6.3f1, from the user's screenshots):**
  * `Script error (TweenRunner): Start() can not take parameters`. An internal method was called `Start`, which
    Unity reserves for its start-up message. Renamed; `UnityLint` now checks every message method.
  * Popups dimmed the screen with a dark oval (a rounded sprite stretched full screen). Now a full-screen dim.
  * Button icons overlapped their labels (Map, Replay, and the hidden play icon on "Next Level"): the label
    resized itself after the row had positioned it. The row now sizes icon and label itself.
  * Settings (and Shop) lists were 100 px wider than the screen, so rows touched the edges and section titles
    were clipped.
  * The Level Complete coin counter covered the title, and the card had a large empty gap; the counter now
    sits above the card and result, mode and message popups size themselves to their content (the coin
    ledger's text ran over its title and buttons, and the Weekly Rush card could run under Play).
  * The map title had level nodes showing through it; the header is now solid, and path dots no longer run
    through star rows or the PLAY label.
  * ✓ and ★ used in some text aren't in the Fredoka font; replaced, and `UnityLint` checks font coverage.
  * The coin ledger showed internal keys (`c.star.4.1`); it now reads "Level 4, star 1".
* **Found by the first real Unity run (6000.6.3f1): 148 compile errors and Safe Mode.** The project had no
  package manifest, and the default one Unity 6.6 generated doesn't include Unity UI. Fixed by shipping
  `Packages/manifest.json`, making the game assemblies wait for Unity UI instead of failing to compile, adding
  a small editor script that installs missing packages, and a test that checks the manifest.
* Bootstrap used `Input.multiTouchEnabled`, which throws in Input-System-only projects. Now guarded.
* `GetComponent() ?? AddComponent()` would fail in the Editor (Unity's "fake null"). Replaced everywhere.
* The coin counter read its label inside `OnEnable` before it was built. Initialisation reordered.
* The map's scrolling content drew over its own header. Draw order fixed.
* Pausing during the Weekly Rush 3-2-1 countdown could leave the timer stopped. Fixed with a safety net.
* Quitting/restarting could leave the challenge "run active" flag set (blocking the shop). Runs now close
  exactly once.
* Swap/Colour Shift lost the "first tile" highlight after a mis-tap; the targeting hint covered the top row;
  the campaign progress bar implied stars beyond the target. All fixed.

## What has NOT been verified

* **Only screenshots from the user's Unity 6.6 editor have been seen** (home, a level 1 win, the map,
  settings and the coin ledger), and the fixes made from them haven't been seen running yet. Nothing has been
  tested on a phone. Layout, animation timing, readability on small
  screens, touch feel, audio mix and performance on real phones are untested.
* Unity versions: code was compile-checked against 2021.3 reference assemblies (uGUI from 2020.3); no Unity 6
  reference assemblies were available here. The project targets Unity 6000.6.3f1 and only uses APIs that are
  unchanged in Unity 6, but it hasn't been opened in that editor by me.
* Packages: `Packages/manifest.json` was written by hand. Unity's package registry couldn't be reached from
  this environment, so the listed versions weren't resolved by a real Package Manager; Unity replaces
  versions that are locked to the editor (such as Unity UI 2.0.0) with its own.
* Android build, signing, IL2CPP stripping, haptics on hardware and safe areas on notched phones.
* The new Input System path (Active Input Handling set to "Input System Package (New)") has been compile-checked
  and checked against the Input System's source (module, keyboard and Back key names), but not run. The old
  Input Manager path is the one seen working in the editor.

## Manual test checklist (please run in Unity, then on a phone)

Use **Settings → Developer** (Editor/dev builds) to add coins, unlock levels and advance the clock.

### First launch and home
- [ ] Splash shows, then the home screen with logo, Play button, Map, three challenge cards, Shop, How to Play, settings gear and coin counter.
- [ ] Every button does something: Play starts level 1; Map opens the map; each card opens its mode card; Shop and How to Play open; gear opens Settings; the coin **+** opens the Shop's Coins tab.

### Campaign and map (§21: map redesigned, scrollable, nodes reflect unlocks and stars)
- [ ] Level 1 shows the hand pointer hint; tapping a group clears it, tiles fall and refill smoothly.
- [ ] Press and hold shows "+points"; sliding to another group previews that one; sliding off cancels.
- [ ] Single tiles shake and cost no move.
- [ ] Reaching the target ends the level with stars, spare-move bonus and coins flying into the counter.
- [ ] "Next Level" plays level 2; "Map" shows the path lighting up to the new level with an unlock animation.
- [ ] The map scrolls smoothly, opens near the current level, shows stars on completed nodes, locks on locked ones, region banners and milestone gems; "My level" appears when scrolled away.
- [ ] Replaying level 1 earns no first-clear coins again (§21) and never lowers stars.
- [ ] Level 8 introduces stones; tiles above a stone don't fall through; sections refill separately.

### Symbols (§21)
- [ ] The eye button in the game toggles shapes on every tile instantly; the button changes style.
- [ ] The setting matches in Settings and Pause, and survives quitting and reopening the app.

### Shop and economy (§21)
- [ ] Shop opens from home; Power-ups and Coins tabs switch.
- [ ] Buying with enough coins deducts the price and increases "Owned"; without enough, it explains the shortfall and nothing changes.
- [ ] Power-ups are locked before their level and unlock at 10/20/30/40/50/60 with a "New Power-up!" card and free uses.

### Power-ups in play
- [ ] Each of the six works as described in DESIGN.md §7; Cancel and Back exit targeting without using anything.
- [ ] Out of moves offers Extra Moves only as an explicit choice (once unlocked).

### Modes (§21: timers)
- [ ] Endless: no timer, a new board every run, bonus moves for groups of 6+, run summary with bests.
- [ ] Daily: no timer, 20 moves, same board when reopened the same day, a new one after "+1 day"; reward once per day.
- [ ] Weekly Rush: 3-2-1-GO, a 60-second countdown, board hidden while paused, play stops at zero, tier rewards once per week.
- [ ] Daily and Weekly show no power-up bar and the shop refuses purchases mid-run (§21).

### Robustness (§21)
- [ ] Android Back closes popups, pauses games, goes back, and asks before quitting from home.
- [ ] Backgrounding the app pauses the game; nothing is lost when the app is killed and reopened.
- [ ] Settings → Reduced motion visibly calms effects; volume sliders and vibration work.
- [ ] Nothing overlaps the notch or gesture bar on a modern phone; text is readable on a small phone.
