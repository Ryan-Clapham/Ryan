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
| Automated tests (NUnit, identical to Unity's EditMode tests) | `dotnet test Tests` | **273 passed, 0 failed** |
| Unity message methods (`Start`, `Update`…) have the signatures Unity accepts; no saved field has a type Unity can't serialize (UAC1001); audio has an AudioListener; every character in on-screen text exists in the Fredoka font | `dotnet run --project UnityLint` | No problems |
| Balance: bots play campaign levels 1–130, Endless, Weekly Rush and 7 Daily Challenges with the real engine | `Sim -- balance` | See DESIGN.md §3–6 |
| Campaign mechanics: Casual and Planner bots play every level 1–200 (24 games each) and flag levels that look too easy, too hard or unfair | `Sim -- campaign 1 200 24 <csv>` | See DESIGN.md §3.1 and docs/campaign-simulation.csv |
| Economy: a simulated player earns coins for 30 days through the real reward code | `Sim -- economy` | See DESIGN.md §8 |
| Art: every generated sprite and a composited mock board exported to PNG and inspected | `Sim -- art` | Reviewed; symbols changed after review (§ below); new-mechanics mock board reviewed (chain padlock moved off the colour symbol) |
| Audio: every effect and the music loop synthesised and checked for NaN/clipping/silence | tests + `Sim -- art` | Pass |

\* The Android reference set omits the mobile-only `Handheld` API, so a harness-only shim declares it;
real Android builds provide it.

### Automated test coverage (273 tests)

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
| Saves and settings | 14 | Full round trip, missing/corrupt/backup/temp recovery on a real file system, write failure, repair of inconsistent data, reload through the context, reset rebinding; sound toggles restore the chosen volume |
| Navigation | 5 | Back stops at Home, no duplicates, no loops, leaving a game replaces it, Home clears history |
| Procedural assets | 6 | Atlas has every referenced sprite with no overlaps, cache round trip, **symbols measurably distinct**, valid audio |
| Regression checklist | 13 | One test per logic item in the brief's §21 list |
| Project setup | 2 | `Packages/manifest.json` declares every package and module the assemblies need |
| Golden gameplay | 5 | Fingerprints of campaign levels, seeded bot games, Endless/Daily/Rush and scripted power-ups. Levels 1–20, the challenge modes and power-ups still match the values recorded before the overhaul; levels 21+ were re-recorded when the mechanics arrived (a deliberate gameplay change was confirmed to break them) |
| Ice | 12 | Frozen tiles join no group and can't be tapped; several cleared neighbours take one layer; thick ice needs two actions; the freed tile falls in the same action; it holds up the tiles above and the gap below refills from beneath; no Swap or Colour Shift; Tile Breaker strips all layers; Blast and Colour Burst take one layer even with neighbours removed |
| Chains | 7 | Chained tiles join their colour's group, stay and lose a link; a group of only chained tiles is a legal move; adjacent clears do nothing; no Swap, but Colour Shift recolours (chain stays); Tile Breaker strips all links; Blast/Colour Burst take one |
| Obstacle boards and layouts | 6 | Recovery reshuffles only free tiles (40 seeds); obstacle notation round trips; validation; obstacles don't change generated colours; obstacle points configurable; `TileRules` matches the definitions |
| Campaign-only mechanics | 7 | Endless, Daily, Weekly and Practice have no obstacles or goals and refuse to start with them; built-in levels 1–300 unchanged; a hand-made level can list obstacles and goals |
| Goals | 8 | Score-goal fallback; tracker counting (stones never count, only last layer/link counts); collect by any means; win the moment every goal is met (with spare-move bonus and at least one star); fail when moves run out with a goal unmet whatever the score; Extra Moves revive a goal level |
| Planner bot | 6 | Picks goal progress over the biggest group; breaks the ice/chains a goal needs; looks one move ahead; deterministic; beats Casual on levels 1–40 (120/120 vs 93/120) and on an ice level (29/30 vs 10/30) |
| Save compatibility | 2 | A real save written by the pre-overhaul build (b0511bc) after 12 campaign levels, Daily, Weekly Rush, Endless, a purchase and settings changes: it loads and writes back byte for byte, and the same play on the new code produces exactly the same save (progress, stars, coins, ledger, records) |
| Special tiles | 29 | Creation at 6/9/12 tiles and only as taught; configurable thresholds; row vs column rockets; placement (tapped cell, or nearest cleared cell); they fall and stop at stones; rocket, bomb and prism hits on every tile type; every combination; chain reactions in order, each once; goals count their clears; Blast, Tile Breaker, Swap, Colour Shift and Colour Burst with specials; a special is always a legal move |
| Treasure | 9 | Falls and is collected at the bottom edge and on stones, never through them; waits on ice, chains, moss and tiles; stacked treasure; untouched by power-ups and reshuffles; layout rules; the treasure goal wins the level |
| Moss | 11 | Leaves with its group and with area hits; scraped off by a neighbouring clear; grows by one free tile after a move that clears none, repeatably, never two moves in a row, never past its cap, never onto ice, chains, specials or treasure; falls with its tile; can't block every move; the moss goal follows the board |
| Campaign design and level files | 20 | No mechanic before its introduction (levels 1–200); each introduction teaches one thing with a tutorial; practice levels focus on it; generated levels valid with goals that can be met and moves in their band; same on every device; levels saved to JSON load back exactly and play the same board (13 levels covering every mechanic); hand-written level files load and bad ones are refused; progress on mechanic levels saves and loads |
| Long play, every mechanic | 1 | 90 bot games on campaign levels 21–200 with random power-ups (about 2,000 actions, 170+ specials made, combos, treasure, moss growth): score bookkeeping, one step per obstacle per action, ice and chains never move, treasure conserved, moss limits, goal counts, board invariants |
| Long play with obstacles | 1 | 60 random 7×7 levels with ice, chains, stones and voids, about 3,100 bot actions with random power-ups: one step per obstacle per action, points = action + obstacle steps, obstacles never move, goal counts exact, board invariants hold, and reshuffles happen with obstacles in place |

### Defects found and fixed during verification

* Symbols: the original blue circle and purple diamond/square were too similar to the heart (measured).
  Blue became a ring and purple a crescent moon (now ≥ 0.46 Jaccard distance from every other shape).
* **Found in later Unity sessions:** shop buttons showed no prices (an empty button label never got a Text);
  warning UAC1001 for `TileView.Cell` (a runtime-only field Unity tried to serialize; now `[NonSerialized]`, and
  `UnityLint` checks for the rest); Unity opened an empty "Untitled" scene (only a sky in the Game view) instead
  of the game's scene, which now opens automatically. Opening the real scene then showed **no sound at all**
  ("There are no audio listeners in the scene"): the game never created an AudioListener and had only worked
  because the Untitled scene's camera carried one. Android builds use the real scene, so they would have been
  silent. The UI camera now carries the listener, and `UnityLint` checks for it.
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
* **The new tile mechanics (phase 2) have not been seen running.** Their rules are tested in the .NET harness and
  their levels simulated, and the new Unity code (tile overlays and badges, special-tile effects, treasure and
  moss animations, the goals panel, prism colour picker, tutorial cards, how-to-play pages, sounds) compiles
  against Unity's assemblies in all four variants and passes the lint (including the font check). But nobody has
  watched it: layout of the goals panel on real screens, animation timing, how the art reads at phone size and
  how the new sounds mix are unverified. The texture atlas grew to 2048 × 1536 (12 MB) to hold the new art.
* Balance comes from bots, not people. 30 of levels 1–200 are flagged by the report (DESIGN.md §3.1).

## Manual test checklist (please run in Unity, then on a phone)

### New tile mechanics (phase 2)

Use **Settings → Developer → unlock levels** to jump ahead, then open each introduction level from the map.

* [ ] **Level 21 (frozen tiles):** a "Something new!" card explains ice, then a pointer lands on a frozen tile.
      Frozen tiles can't be tapped (a tip appears); clearing a group next to one cracks it (sound, shards) and,
      once free, it falls. The goals panel at the top shows the ice left and ticks when done.
* [ ] **Level 25 (collect a colour):** the goal shows a blue tile and a count; any blue cleared counts.
* [ ] **Level 28 (thick ice):** thick ice has a double rim and a "2" badge; the first crack turns it to normal ice.
* [ ] **Level 31 (rockets):** the card, then a pointer at the ready-made rocket. Press and hold it to preview its
      row; release to fire (beam across the row, one move used). Clear the 8 blue tiles at the bottom: a rocket
      appears where you tapped, and a tip says to tap it. Check row rockets (arrows left-right) and column rockets
      (arrows up-down).
* [ ] **Level 41 (bombs)** and **level 47 (combos):** a bomb clears a 3 × 3 square; tapping one of the two touching
      rockets fires both (row and column at once).
* [ ] **Level 51 (prisms):** tap the prism: a colour row replaces the power-up bar and the banner asks for a
      colour. Pick a swatch, or tap any tile of that colour; Cancel backs out without using a move.
* [ ] **Level 61 (chains):** chained tiles show chains, a padlock and a link count from 2; they match their colour,
      stay in place and lose a link when their group clears.
* [ ] **Level 71 (treasure):** clear the tiles under a chest; it falls, and is collected (sparkle, sound, +500) when
      it reaches the bottom.
* [ ] **Level 101 (moss):** mossy tiles clear with their group or when a group next to them clears; after a move
      that clears no moss, it grows onto one neighbour (never two moves running).
* [ ] **Power-ups with the new tiles:** Blast and Tile Breaker set off a special; Swap moves a special with a tile;
      Colour Shift recolours a chained tile but not a frozen one; Tile Breaker strips ice or chains in one go.
* [ ] **Losing a goal level:** the Out of Moves card lists what is still to do.
* [ ] **Level popup** on the map shows the goals instead of a target score for goal levels.
* [ ] **How to Play** has two new pages (goals and obstacles, special tiles).
* [ ] Toggle **Symbols**: tiles under ice, chains and moss still show their symbol, and the goals panel updates.
* [ ] Daily Challenge, Weekly Rush and Endless look and play exactly as before (no obstacles, no specials).

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
