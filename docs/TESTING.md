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
| SDK integrations (AdMob + UMP, Unity IAP 5) compile against **API shims written from the SDKs' documentation**, not the real SDKs (which couldn't be downloaded here), Android and iOS paths | `dotnet build SdkCheck`, `-p:UnityPlatform=iOS` | 0 errors, 0 warnings (shims only: docs/MONETISATION.md §11) |
| Automated tests (NUnit, identical to Unity's EditMode tests) | `dotnet test Tests` | **453 passed, 0 failed** |
| Unity message methods (`Start`, `Update`…) have the signatures Unity accepts; no saved field has a type Unity can't serialize (UAC1001); audio has an AudioListener; every character in on-screen text exists in the Fredoka font | `dotnet run --project UnityLint` | No problems |
| Balance: bots play campaign levels 1–130, Endless, Weekly Rush and 7 Daily Challenges with the real engine | `Sim -- balance` | See DESIGN.md §3–6 |
| Campaign mechanics: Casual and Planner bots play every level 1–200 (12 games each, every move played) and flag levels that look too easy, too hard, unfair, trivial or short of three stars | `Sim -- campaign 1 200 12 <csv>` | See DESIGN.md §3.1–3.2 and docs/campaign-simulation.csv |
| Mechanics in other modes: Daily success by unlock level (60 days), Weekly Rush medal rates with and without mechanics (16 weeks), Endless stage journey by bot (30 runs each) | `Sim -- modes` | See DESIGN.md §4–6 |
| Economy: a simulated player earns coins for 30 days through the real reward code | `Sim -- economy` | See DESIGN.md §8 |
| Art: every generated sprite and a composited mock board exported to PNG and inspected | `Sim -- art` | Reviewed; symbols changed after review (§ below); new-mechanics mock board reviewed (chain padlock moved off the colour symbol) |
| Audio: every effect and the music loop synthesised and checked for NaN/clipping/silence | tests + `Sim -- art` | Pass |

\* The Android reference set omits the mobile-only `Handheld` API, so a harness-only shim declares it;
real Android builds provide it.

### Automated test coverage (453 tests)

| Area | Tests | Highlights |
|---|---|---|
| Scoring | 10 | 2→40, 3→90, 4→160, 5→250, 6→360; configurable formula; praise tiers; star thresholds |
| Group detection | 6 | Orthogonal only, diagonals don't connect, stones/voids break groups, buffer reuse |
| Gravity and refill | 6 | Ids preserved, stones stop falls, **independent sections**, voids as barriers |
| Dead-board recovery | 7 | Reshuffle keeps the same tiles, stones never move, constructive fallback, graceful failure, determinism |
| Sessions and modes | 14 | Win/lose/revive, Daily plays all moves, Endless bonus moves, Rush timer start/stop/expiry/pause, **only Weekly Rush is timed**, long randomised play keeps board invariants |
| Power-ups | 17 | All six effects and their invalid targets, stones vs Blast/Tile Breaker, trial-first consumption, nothing consumed on failure, **challenge modes blocked at engine and service level** |
| Wallet, shop, rewards | 23 | Ledger, insufficient funds, locked items, **purchases blocked during challenge runs**, first-clear once, new stars only, milestones once, daily/weekly/endless claims once, clock rollback refused, early-economy sanity; a power-up's free uses are lent on every attempt at its level (also after reopening the game), spent first, never added to the inventory or saved |
| Progression | 9 | **Board sizes 6×6 / 7×7 / 8×9**, 120 generated levels valid and deterministic, stones from level 8, early levels ≥80% winnable by a casual bot, replays never reduce stars |
| Challenges | 15 | UTC day keys, ISO weeks (including year boundaries), Monday reset, same board and refills for the same date/week, different days differ, **Endless fresh board each run** |
| Saves and settings | 14 | Full round trip, missing/corrupt/backup/temp recovery on a real file system, write failure, repair of inconsistent data, reload through the context, reset rebinding; sound toggles restore the chosen volume |
| Navigation | 5 | Back stops at Home, no duplicates, no loops, leaving a game replaces it, Home clears history |
| Procedural assets | 6 | Atlas has every referenced sprite with no overlaps, cache round trip, **symbols measurably distinct**, valid audio |
| Regression checklist | 13 | One test per logic item in the brief's §21 list |
| Project setup | 2 | `Packages/manifest.json` declares every package and module the assemblies need |
| Golden gameplay | 5 | Fingerprints of campaign levels, seeded bot games, Endless/Daily/Rush and scripted power-ups. Levels 1–20, the challenge modes and power-ups still match the values recorded before the overhaul (seeded games finish the moment a level is passed, which reproduces the old end of level exactly); levels 21+ were re-recorded for each deliberate change to them; the scripted power-up fingerprint was re-recorded for 3.0.1 because its Extra Moves step went with the retired power-up, and the 3.0 build gives the same value with only that step removed (all six fingerprints compared) |
| Ice | 12 | Frozen tiles join no group and can't be tapped; several cleared neighbours take one layer; thick ice needs two actions; the freed tile falls in the same action; it holds up the tiles above and the gap below refills from beneath; no Swap or Colour Shift; Tile Breaker strips all layers; Blast and Colour Burst take one layer even with neighbours removed |
| Chains | 7 | Chained tiles join their colour's group, stay and lose a link; a group of only chained tiles is a legal move; adjacent clears do nothing; no Swap, but Colour Shift recolours (chain stays); Tile Breaker strips all links; Blast/Colour Burst take one |
| Obstacle boards and layouts | 6 | Recovery reshuffles only free tiles (40 seeds); obstacle notation round trips; validation; obstacles don't change generated colours; obstacle points configurable; `TileRules` matches the definitions |
| Campaign-only mechanics | 7 | Endless, Daily, Weekly and Practice have no obstacles or goals and refuse to start with them; built-in levels 1–300 unchanged; a hand-made level can list obstacles and goals |
| Goals | 11 | Score-goal fallback; tracker counting (stones never count, only last layer/link counts); collect by any means; meeting every goal passes the level and play goes on; Finish ends a passed level with nothing added for unused moves and is refused before passing; a passed level that runs out of moves is won; fail when moves run out with a goal unmet whatever the score; Extra Moves revive a goal level |
| Stars and objectives (play on) | 13 | The level 67, 47 and 78 reports: the target passes without ending and stars rise with every move left (the HUD's stars are the stars awarded); the real level 67 keeps going after its goals; a high score with a treasure left fails, Extra Moves keep score and progress, a power-up finishes the goal, Finish and the saved result; the combinations tutorial is no longer a one-tap win; no level 21–60 is passed by move 2; three stars within reach on 21–60; make/set off/combination goals count only the kind asked, chain reactions, both halves of a combination and power-ups; all goals needed; the new goals round trip through level files; star requirements match the star rule on levels 1–60; the result says why the next star was missed; stars from playing on are saved |
| Planner bot | 6 | Picks goal progress over the biggest group; breaks the ice/chains a goal needs; looks one move ahead; deterministic; beats Casual on levels 1–40 (120/120 vs 93/120) and on an ice level (29/30 vs 10/30) |
| Save compatibility | 3 | A real save written by the pre-overhaul build (b0511bc) after 12 campaign levels, Daily, Weekly Rush, Endless, a purchase and settings changes: it loads and writes back byte for byte; the same play on the new code (every move played) makes exactly the same progress: levels unlocked and completed, plays and wins, first-clear and milestone rewards, Daily and Weekly plays, the purchase, inventory, tutorials and settings (scores, stars and star rewards differ, because levels are now played to the end and unused moves score nothing); and the whole save it writes matches a recorded fingerprint, which the build before the 2.1 clarifications also produced byte for byte (checked in a separate worktree); since 3.0 the save also has a monetisation record, which an old save gets empty, and the rest of the save still matches the 2.1 fingerprint exactly |
| Special tiles | 29 | Creation at 6/9/12 tiles and only as taught; configurable thresholds; row vs column rockets; placement (tapped cell, or nearest cleared cell); they fall and stop at stones; rocket, bomb and prism hits on every tile type; every combination; chain reactions in order, each once; goals count their clears; Blast, Tile Breaker, Swap, Colour Shift and Colour Burst with specials; a special is always a legal move |
| Treasure | 9 | Falls and is collected at the bottom edge and on stones, never through them; waits on ice, chains, moss and tiles; stacked treasure; untouched by power-ups and reshuffles; layout rules; the treasure goal wins the level |
| Moss | 11 | Leaves with its group and with area hits; scraped off by a neighbouring clear; grows by one free tile after a move that clears none, repeatably, never two moves in a row, never past its cap, never onto ice, chains, specials or treasure; falls with its tile; can't block every move; the moss goal follows the board |
| Campaign design and level files | 22 | No mechanic before its introduction (levels 1–200); each introduction teaches one thing with a tutorial; practice levels focus on it; obstacle combinations arrive one showcase at a time and most later obstacle levels mix kinds; objective-only, score and combined levels all exist; generated levels valid with goals that can be met and moves in their band; same on every device; levels saved to JSON load back exactly and play the same board (13 levels covering every mechanic); hand-written level files load and bad ones are refused; progress on mechanic levels saves and loads |
| Mechanics in every mode | 24 | Each of the seven mechanics gives identical results (and points) in Campaign, Daily, Weekly Rush and Endless; power-up rules per mode unchanged; recovery keeps obstacles in every mode; campaign and Endless unlocks (milestones configurable); Daily uses only the day's plan and the player's unlocks, is identical for players with the same unlocks, stays keyed by date, needs its goals as well as the target, and its goals are reachable (planner 27/30 days); Weekly themes, more combinations than Daily, never treasure with chains, medal rewards follow the board played; players with nothing unlocked get the original challenges and Endless; Endless level saved and migrated from old saves; the Endless journey brings one thing per stage (stones, ice, a breather, chains, rockets and bombs, treasure, a breather, moss, prisms) then mixes, more kinds and denser, with breathers; waves arrive only with new stages, never more stones than columns; no specials before their stage; random play in every mode keeps every rule; the star checklist |
| Scoring, stones and the finale (2.1) | 49 | Rocket scores on rows 5–10 wide and columns 6–12 tall; a rocket scores only what it reaches (holes nothing, stones and ice their points); **the bomb's 5 × 5 square is centred on the bomb in the middle, on all four edges and in all four corners (25 cells in the middle, 16 one step from a corner, 15 on an edge, 9 in a corner), never reaches off the board, and reaches 25 cells in the middle of boards from 5 × 5 to 10 × 12**; even-sized squares (config) still lean to the middle; bombs break stones and hit obstacles in their square, scoring each once; only specials and Tile Breaker break stones (not groups, Blast, Colour Burst or prisms), and a broken stone lets tiles fall through; every combination pays 75 a tile with the combo bonus as its own line; two prisms clear the board for a big score but don't pass a level with thick ice and chains; chain reactions score each tile once; specials left when the moves run out go off (no move charged, points and goal progress count, a goal they finish still passes), **adjacent ones together as a combination**, in Campaign, Daily and Endless but not Weekly Rush; a failed finish stays failed and Extra Moves restart it; **Finish sets the specials off and adds nothing for unused moves**; every power-up's points and goal progress; tutorial power-ups kept on the first win only, temporary on replays, gone on the next level, kept through a revivable failure, settled across a restart, nothing extra for players with older free trials; the tutorial hand's fingertip lands on its target on 9:16, 9:20, 3:4 and 9:19.5 screens and the hand stays on screen |
| Groups of specials (2.1) | 17 | Three rockets (a cross three lines wide), four rockets (five wide), three bombs (9 × 9, clipped), a rocket and two bombs (a five-line band), two rockets and a bomb (a three-wide cross), prism + rocket + bomb (the colour, then the band), two prisms + a bomb (every colour tile, and the bomb's area breaks stones), a snake of five specials fires as one; only orthogonal neighbours join (a diagonal special fires later, alone); a blast that reaches a special outside the group sets it off once, alone, with no loop; power-ups that hit specials set them off singly and never combine them; ice, chains, moss and stones in a combination's area each take one step and score once; make/fire/combination goals count every member once; the preview shows exactly the cells the combination clears; a group of three left at the end goes off as one combination; Finish uses the same groups; 200 random boards full of specials: every special fires at most once, no tile scores twice, score = breakdown, the session always settles |
| Monetisation (3.0) | 68 | **With scripted fakes in place of the ad, consent and store SDKs** (not the SDKs themselves): a rewarded advert grants its reward only on the SDK's reward callback, exactly once (duplicate callbacks ignored), with exactly +10 moves or +50 coins and the balance saved at once; closed early, no reward callback, failed to show, failed to load (retried with growing delays up to 5 minutes), never answered, still open after the player is back, callbacks arriving after the game resumes, a reward just after the close (counts) or long after (granted once, reported); daily limit, cooldown and per-attempt limit survive a restart and a clock wound back; one advert at a time; Google's test ad units used and no live IDs built in; new opportunities added without changing the service. Out of Moves: +10 moves by advert and +5 for exactly 100 coins keep every tile, obstacle, score, goal and power-up; nothing taken without enough coins; an interrupted advert leaves the choice open; campaign only. Shortfall: the exact shortfall up to 50, explained when not enough, never buys anything. Interstitials: none before 5 completed levels, then at the next level or map only after a win, never after a failure, retry, abandoning or during play; 5 minutes apart; the count kept when none is loaded or one fails and reset only when one is displayed; none within 2 minutes of a rewarded advert, a coin pack or a coin purchase; none (not even loaded) with No Ads, while rewarded adverts stay; the count survives a restart. Consent: no ad SDK before consent, an earlier session's consent starts it at once, no consent or a consent error means no adverts while coins and purchases still work, privacy options can start adverts, settings reach the SDKs. Purchases: prices only from the store; granted, saved and only then confirmed; never granted twice (duplicates, redelivery after a crash); left unconfirmed if the save fails; cancelled, failed and pending purchases change nothing until paid; purchases blocked when they should be; a store that never answers unlocks the shop; unknown products neither granted nor confirmed; No Ads survives a restart (also offline), is restored on a new device and by Restore Purchases (a restore the store never answers ends as failed after 30 s), and a refund is applied after two store checks. Saves: old saves load with empty monetisation data, the record round trips and is capped, a progress reset keeps purchases, starting monetisation changes no progress; ledger reasons readable; 300 rounds of random callback storms never pay more than once per rewarded advert |
| Retired power-up (3.0.1) | 6 | Extra Moves is in no list, bar, shop or introduction (level 50 introduces nothing; the others are unchanged) and can't be bought; an older config asset that still lists it is ignored; owned uses are paid back once at 200 coins each (or the price the config charged), saved at once, with a readable ledger line and never paid twice; free trial uses lapse without a refund; a save that never had it is not touched or rewritten. Also (in other suites): the retired power-up can't be applied, even to revive an out-of-moves level, and challenge modes refuse it |
| Long play, every mechanic | 1 | 90 bot games on campaign levels 21–200 with random power-ups (about 3,400 actions, 400 specials made, combos, treasure, moss growth, 47 finales): score bookkeeping, the score breakdown always equals the score, no tile removed twice, one step per obstacle per action, ice and chains never move, treasure conserved, moss limits, goal counts, board invariants |
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
* **The 3.0.1 layouts have not been seen running.** The new Level Complete layout, the No Ads card and the Best value
  badge compile and fit on paper: the results popup is at most about 1,424 units tall, against at least 1,592 available
  on the shortest supported screen. That arithmetic covers each supported aspect ratio, but nobody has looked at the
  screens yet.
* **Update 3.0 (adverts and purchases) has not been seen running, and no real SDK has been used.** The Google Mobile
  Ads, consent and Unity IAP integrations have only been compiled against API shims written from the SDKs'
  documentation (the real packages couldn't be downloaded in this environment); the first real compile is when the
  project is opened with the SDKs installed. Not done: Google's test adverts on a device, the consent form, Google
  Play Billing with license testers, real adverts or payments, and the screens themselves (Out of Moves offers,
  Coins and Premium tabs, the buy screen's advert offer, Settings > Privacy options, the input blocker and paused
  audio during adverts). The device test plan is in docs/MONETISATION.md §11.
* Balance comes from bots, not people. 43 of levels 1–200 are flagged by the report, 13 of them *unfair?* (the
  Planner passes under 80%, most at 9 of 12 games) and 2 clearly hard for it, 99 and 151 (DESIGN.md §3.1). The bomb
  size decision (DESIGN.md §3.4) also rests on bot games.
* **The 2.1 clarifications have not been seen running:** the 5 × 5 bomb's flashes (every hit cell lights up,
  staggered by distance, with the ring sized to the area), the flashes and beams of groups of three or more
  specials and the "3x Combo!" preview, the "Specials at the end" card that lists the end-of-level points (its size
  on a short phone, and that it doesn't hide the board for too long: it stays 2.4 s), the result screen's "Specials
  at the end" note, and the Finish confirmation's new wording all compile in every variant and pass the lint, but
  their look and timing are unverified. The rules behind them are tested in the harness.
* **Update 2.1 has not been seen running:** the end-of-level finale animation and its banner, the bomb ring,
  stones breaking under rockets and bombs (and the tiles falling through), stones and specials appearing in Endless
  stages and their banners, the score breakdown grid on the result screen, the coin balance on the Out of Moves
  screen (and the "coins left" toast after buying moves), the "Kept 1 free ..." badge, and the corrected tutorial
  hand all compile in every variant and pass the lint, but their look and timing are unverified. The hand's
  placement is tested as arithmetic at four aspect ratios, not on a screen.
* **The play-on update has not been seen running:** the Finish button in the top bar, the stars under the score
  (goal levels) or on the bar (score levels), the "Level passed!" banner, the Finish confirmation, the longer level
  card with its star requirements, the result checklist and "why" line, and the "Power-up practice" card all compile
  in every variant and pass the lint, but their layout on real screens is unverified. Level generation now takes
  about 17 ms on average in the harness (was 6 ms); on a phone the first opening of a level card may pause briefly.

## Manual test checklist (please run in Unity, then on a phone)

### Update 3.0.1: results layout, shop cards, Extra Moves retired

* Level Complete, on a 9:16 phone, a tall phone and a tablet, with a level that scores in many categories (so the
  breakdown has four rows), on a first clear (coins earned):
  * the stars and the final score stand out ("New best score!" in gold under it when it is one);
  * then the three star rows, the two-column breakdown, one "Coins earned" row (coins fly into the counter above the
    popup), Next Level, and Map / Replay;
  * nothing is cut off, overlaps or needs scrolling.
* Finish a level early with fewer than three stars: a one-line "You finished with N moves left." appears under the
  star rows.
* Shop > Premium: the No Ads description sits above the button with space between them, in all three states
  (store unavailable in the editor: "Unavailable"; "Connecting to the store..."; "Owned" after buying). The
  "Permanent purchase" badge is a full pill under the name.
* Shop > Coins: "Best value" is a pill between "2,750 coins" and "Chest of Coins", clear of the price button.
* A save with Extra Moves owned: a "has been retired: +N coins" message at start-up; the shop and bar no longer show
  it; the coin ledger (developer tools) shows the refund.
* The Unity console no longer shows warning UAC0005 for MonetisationSdkSetup.cs.

### Update 3.0: adverts, purchases, consent

Run on an Android phone with a development build (test adverts), and the purchases with a Play Console license
tester; the full plan is docs/MONETISATION.md §11. In the editor, Google's placeholder adverts and Unity IAP's fake
store only show that the screens react; they are not a test of the real flow.

* First start: the consent form (with `privacy.debugGeographyEea` and your test device ID) appears over the home
  screen; after answering, Settings shows Privacy options when required.
* Out of Moves: "Watch an advert: +10 moves" plays a test advert; the level continues with the same board and 10
  moves; closing it early adds nothing and the choices stay. "+5 moves for 100" works with enough coins and explains
  the shortfall without. "Give Up" shows Try Again / Map / Home. The game's music pauses during the advert and taps
  don't reach the board; the pause menu doesn't open.
* Shop > Coins: the advert card (+50, adverts left today), four packs priced by Google Play ("..." while connecting),
  balance updating at once. Shop > Premium: No Ads with its price, then "Owned"; Restore Purchases.
* An unaffordable power-up (shop or in-level bar): the buy screen offers "Watch an advert: +N coins" for the exact
  shortfall (at most 50); afterwards Buy is enabled and nothing was bought until it's tapped.
* Interstitials: after 5 won levels, one test interstitial on Next Level or Map; none on Replay or after a loss;
  none again for 5 minutes; none with No Ads.
* Check the new texts and prices (with currency symbols) fit and render on a short phone.

### Update 2.1: scoring, stones, finale, tutorials, Endless

* Fire a rocket across a full 8-wide row: about 350 points (7 tiles × 50), more on a longer row. A bomb in the
  middle clears the 5 × 5 square centred on it (24 tiles, about 1,200 points); on an edge or in a corner the square
  is cut off (14 or 8 tiles) and the flashes cover exactly the cells that went. Two specials side by side pay 75 a
  tile; two bombs clear 7 × 7.
* A rocket or bomb reaching a stone breaks it (100 points) and the tiles above fall through. Groups, Blast and
  Colour Burst never break stones; Tile Breaker does.
* Run out of moves with a rocket on the board: "Out of moves! Your special goes off" appears, the rocket fires, a
  "Specials at the end" card lists its points, then the result shows. If its blast finishes the last goal, the
  level is passed. With three specials touching, they go off together ("Your 3 specials go off") as one combination.
  Tap Finish with specials on the board: the confirmation says the unused moves score nothing and the specials go
  off first; they do, the card shows, then the result. No spare-move points appear anywhere.
* The level result shows a breakdown (Groups, Special tiles, Combo bonus, Power-ups, Ice/chains/moss, Stones,
  Treasure, Specials at the end) whose numbers add up to the final score, and, when specials went off at the end,
  a "Specials at the end: tiles … · combo bonus …" note with the same figures as the card; check it fits on a
  short phone with the star checklist and rewards.
* Groups of specials: put three or more specials side by side (a line or an L): holding one highlights the whole
  group and the preview reads "3x Combo!"; releasing fires them as one (one bomb sound, flashes on every hit cell).
  A special touching only diagonally is not part of the group. Inventory power-ups never fire together.
* Out of Moves: the coin balance shows at the top of the popup with "Watch an advert: +10 moves", "+5 moves for 100"
  and "Give Up" (update 3.0 checklist below).
* Level 60 (Colour Burst): don't use the free one and win: the result says "Kept 1 free Colour Burst" and the shop
  shows it owned. Replay and win again: nothing more is kept. On level 61 the bar shows no "Free" badge unless the
  player owns one.
* Level 5: the hand points at the eye button itself (not its right edge), on a tall phone and on a tablet. Level
  10/20/30/...: the hand points at the power-up's button.
* Endless: stage 2 adds stones, 3 frozen tiles, 4 a breather (+5 moves), 5 chains, 6 rockets and bombs (a rocket
  appears), 7 treasure, 8 a breather, 9 moss, 10 prisms, then mixes; each with a banner saying what it brings. The
  mode card shows the best stage and the next new thing.

### Passing, playing on and stars

* Level 67 (or any score level): reaching the target shows "Level passed!", the moves badge keeps counting, the bar
  keeps filling towards the third star and its stars light up as they are reached; the top-right button becomes
  **Finish**. Play every move: the result shows the stars of the final score.
* Finish with fewer than three stars: a confirmation shows the score and stars so far, says the moves left score
  nothing, and offers "Keep playing". With three stars, Finish ends at once. The result has no spare-move line; its
  "why" line says how many moves were left unplayed.
* A goal level: three small stars and a caption ("Goals first", then "2nd: 2,800", "3rd: 3,300") sit under the
  score; check they don't collide with the goals on a narrow phone.
* Level 78 (or any treasure level): run out of moves with a high score and a treasure left: "Out of Moves" says
  "Plenty of points, but 1 goal left: bring down 1 treasure." Continuing (advert or coins) keeps the score and goals as
  they were.
* Pause a passed level: the quit button reads **Finish Level** and finishing shows the result. Restart a passed
  level: a toast says it was saved with its stars.
* Level cards: three star rows with their requirements; on a completed level the earned ones are ticked. The card
  is taller than before: check it fits a short phone screen.
* Level 47: tapping the two rockets frees 10 frozen tiles and fires the combination, but the level goes on (4 frozen
  tiles left).
* Replay level 60 (Colour Burst): the "Power-up practice" card appears and Colour Burst shows "Free" on the bar for
  that attempt; use it or not, the inventory count afterwards is unchanged. Close and reopen the game, replay: it
  is lent again.
* Replay an introduction level (21, 31, 47, 61, 71, 101...): its tutorial card shows again.

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
* [ ] **Level 41 (bombs)** and **level 47 (combos):** a bomb clears the 5 × 5 square centred on it; tapping one of the two
      touching rockets fires both (row and column at once).
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
* [ ] **Star checklist:** finish a level with one or two stars: the Level Complete card lists what each star needs,
      ticks the ones earned and says how many points short the others were.
* [ ] **Daily Challenge with mechanics:** with the campaign past level 22, the Daily card may say "Today: frozen
      tiles"; the game shows the goals panel (target first, then the obstacles); the result says what is still to do
      if a goal was missed. With a fresh save the daily looks exactly as before.
* [ ] **Weekly Rush with mechanics:** the Weekly card names the week's theme and its medal scores; the board has the
      theme's tiles (a few rockets, bombs or prisms on special weeks); the result card shows the same medal scores.
* [ ] **Endless progression:** the Endless card shows "Endless level N" and the next unlock. Play runs until level 5
      (Settings → Developer can help): the result shows the level gained and "New in Endless: frozen tiles!". In the
      next run, reaching stage 2 shows a banner naming what appears, frozen tiles appear on the board, and their card
      explains them if never seen before. Close and reopen the app: the Endless level is kept.

Use **Settings → Developer** (Editor/dev builds) to add coins, unlock levels and advance the clock.

### First launch and home
- [ ] Splash shows, then the home screen with logo, Play button, Map, three challenge cards, Shop, How to Play, settings gear and coin counter.
- [ ] Every button does something: Play starts level 1; Map opens the map; each card opens its mode card; Shop and How to Play open; gear opens Settings; the coin **+** opens the Shop's Coins tab.

### Campaign and map (§21: map redesigned, scrollable, nodes reflect unlocks and stars)
- [ ] Level 1 shows the hand pointer hint; tapping a group clears it, tiles fall and refill smoothly.
- [ ] Press and hold shows "+points"; sliding to another group previews that one; sliding off cancels.
- [ ] Single tiles shake and cost no move.
- [ ] Reaching the target shows "Level passed!" and play goes on; when the level ends the result shows the stars and coins fly into the counter.
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
- [ ] Power-ups are locked before their level and unlock at 10/20/30/40/60 with a "New Power-up!" card and free uses; level 50 shows no power-up card; the bar has five buttons (no Extra Moves).

### Power-ups in play
- [ ] Each of the six works as described in DESIGN.md §7; Cancel and Back exit targeting without using anything.
- [ ] Out of moves offers an advert (+10 moves), coins (+5 for 100) and Give Up as explicit choices.

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
